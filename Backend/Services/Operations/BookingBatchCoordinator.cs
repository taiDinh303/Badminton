using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Contract.Services.Operations;
using ModelViews.Booking;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class BookingBatchCoordinator(IUnitOfWork uow, IOperationsAccess access, BookingCreationService creation, IBookingService bookings) : IBookingBatchCoordinator
{
    public async Task<IReadOnlyList<BookingView>> CreateAsync(CreateBookingRequest[] requests, int userId, string key, int? expectedTotal = null)
    {
        await access.ActorAsync();
        if (requests.Length is < 1 or > 20 || requests.Any(r => r.CourtId != requests[0].CourtId))
            throw new BadRequestException("Lịch đặt hàng loạt không hợp lệ.");
        var targetCourt = await uow.GetRepository<Court>().GetByIdAsync(requests[0].CourtId) ?? throw new NotFoundException("Court", requests[0].CourtId);
        if (userId != access.ActorId)
        {
            await access.StaffBranchAsync(targetCourt.BranchId);
            var customer = (await uow.GetRepository<User>().GetByConditionAsync(u => u.Id == userId)).SingleOrDefault();
            if (customer is null || customer.Role != Badminton.Core.Enums.Role.Customer || customer.DeletedAt.HasValue
                || customer.Status != Badminton.Core.Enums.UserStatus.Active || !customer.IsVerified)
                throw new ConflictException("Chỉ đặt hộ tài khoản khách đang hoạt động và đã xác minh.");
        }
        key = OperationInput.Text(key, 60, 1);
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            userId,
            requests,
            expectedTotal
        }));
        int[] ids = [];
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"batch:{access.ActorId}:{key}");
            var batches = uow.GetRepository<BookingBatch>();
            var existing = (await batches.GetByConditionAsync(b => b.ActorId == access.ActorId && b.RequestKey == key)).SingleOrDefault();
            if (existing is not null)
            {
                if (existing.Fingerprint != fingerprint)
                    throw new ConflictException("Mã yêu cầu đã được dùng cho lịch khác.");
                ids = JsonSerializer.Deserialize<int[]>(existing.BookingIdsJson)!;
                return;
            }
            var court = await uow.GetRepository<Court>().GetByIdAsync(requests[0].CourtId) ?? throw new NotFoundException("Court", requests[0].CourtId);
            await uow.LockBookingRequestAsync($"branch-config:{court.BranchId}");
            if (requests[0].GroupId is { } group)
                await uow.LockGroupAsync(group);
            foreach (var day in requests.Select(r => DateOnly.Parse(r.Date)).Distinct().Order())
                await uow.LockCourtDateAsync(court.Id, day);
            await uow.LockPointsUserAsync(userId);
            var created = new List<int>();
            long total = 0;
            foreach (var (request, index) in requests.Select((r, i) => (r, i)))
            {
                var child = request with
                {
                    IdempotencyKey = "batch-" + Hash($"{access.ActorId}:{key}:{index}")
                };
                var booking = await creation.CreateAsync(child, userId);
                created.Add(booking.Id);
                total += booking.PayableAmount;
                await uow.GetRepository<BookingHistory>().AddAsync(new()
                {
                    BookingId = booking.Id,
                    ActorId = access.ActorId,
                    Event = "created",
                    Detail = access.ActorId == userId ? "Khách đặt lịch." : "Nhân viên đặt hộ khách; chờ xác nhận tại quầy."
                });
            }
            if (expectedTotal.HasValue && expectedTotal.Value != total)
                throw new ConflictException("Giá đã thay đổi so với báo giá; chưa tạo đơn nào. Hãy xem lại báo giá.");
            ids = created.ToArray();
            await batches.AddAsync(new()
            {
                ActorId = access.ActorId,
                RequestKey = key,
                Fingerprint = fingerprint,
                BookingIdsJson = JsonSerializer.Serialize(ids)
            });
            await uow.SaveChangesAsync();
        });
        var result = new List<BookingView>();
        foreach (var id in ids)
            result.Add(await bookings.GetByIdAsync(id, access.ActorId));
        return result;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
