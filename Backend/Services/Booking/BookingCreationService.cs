using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Group;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Voucher;
using ModelViews.Booking;

namespace Services;

public sealed class BookingCreationService(IUnitOfWork uow, ICurrentUser currentUser,
    IPricingService pricing, IVoucherService vouchers, IGroupService groups, IPointService points)
    : BaseService(uow, currentUser)
{
    private readonly IPricingService _pricing = pricing;
    private readonly IVoucherService _vouchers = vouchers;
    private readonly IGroupService _groups = groups;
    private readonly IPointService _points = points;
    private IGenericRepository<Booking> Repo => Repository<Booking>();
    public async Task<Booking> CreateAsync(CreateBookingRequest request, int userId)
    {
        request = request with
        {
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            VoucherCode = string.IsNullOrWhiteSpace(request.VoucherCode) ? null : request.VoucherCode.Trim()
        };
        if (request.IdempotencyKey?.Length > 80)
            throw new BadRequestException("IdempotencyKey tối đa 80 ký tự.");
        Booking result = null!;
        await Uow.RunInTransactionAsync(async () =>
        {
            if (request.IdempotencyKey is { } key) await Uow.LockBookingRequestAsync(key);
            result = await CreateCoreAsync(request, userId);
        });
        return result;
    }

    private async Task<Booking> CreateCoreAsync(CreateBookingRequest request, int userId)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = (await Repo.GetByConditionAsync(
                b => b.IdempotencyKey == request.IdempotencyKey)).FirstOrDefault();
            if (existing is not null)
            {
                if (existing.RequestFingerprint is null ||
                    existing.RequestFingerprint != BookingRequestIdentity.Fingerprint(request, userId))
                    throw new ConflictException("IdempotencyKey đã được dùng cho một yêu cầu khác hoặc đơn cũ không có fingerprint. Dùng key mới.");
                return existing;
            }
        }

        var court = await Repository<Court>().GetByIdAsync(request.CourtId);
        if (court is null || !court.IsActive || court.DeletedAt.HasValue)
            throw new BadRequestException("San khong ton tai hoac khong hoat dong.");

        var slot = await Repository<TimeSlot>().GetByIdAsync(request.SlotId);
        if (slot is null || !slot.IsActive || slot.DeletedAt.HasValue)
            throw new BadRequestException("Khung gio khong ton tai.");
        var branch = await Repository<Branch>().GetByIdAsync(court.BranchId);
        if (branch is null || !branch.IsActive || branch.DeletedAt.HasValue || branch.Status != BranchStatus.Active || slot.StartTime < branch.OpenTime || slot.EndTime > branch.CloseTime)
            throw new BadRequestException("Khung giờ nằm ngoài giờ mở cửa của cơ sở.");

        var date = ParseDate(request.Date);

        var dateStatus = await GetCourtDateStatus(court.Id, date, slot);
        if (dateStatus != CourtDateStatus.Open)
            throw new ConflictException("San dang bao tri hoac dong trong ngay nay.");

        var hanoiNow = HanoiTime.HanoiNow();
        var slotStart = HanoiTime.SlotStart(date, slot.StartTime);
        if (slotStart < hanoiNow.AddMinutes(30))
            throw new BadRequestException(
                "Thoi gian nay khong the dat (slot da bat dau hoac trong 30 phut toi).");
        var hanoiToday = HanoiTime.HanoiToday();
        if (date > hanoiToday.AddDays(30))
            throw new BadRequestException("Vui long chon ngay trong 30 ngay toi.");

        var requester = await Repository<User>().GetByIdAsync(userId);
        if (requester is null || requester.DeletedAt.HasValue || requester.Status != UserStatus.Active || !requester.IsVerified)
            throw new ConflictException("Tài khoản chưa được xác minh hoặc đã bị khóa.");
        if (requester is not null
            && requester.NoShowLimited
            && requester.NoShowLimitedPeriod is { } lp
            && lp == HanoiTime.HanoiToday().MonthStart())
            throw new ConflictException(
                "Tai khoan dang bi gioi han dat sau 3 lan ko di (thang nay). Thang tiep se mo lai.");

        if (request.GroupId is { } groupId)
        {
            if (!await _groups.CanBookForGroupAsync(groupId, userId))
                throw new ConflictException("Chi nguoi tao nhom duoc dat cho nhom.");
            if (await CountActiveBookings(userId, groupId, date) >= 8)
                throw new ConflictException("Nhom da dat gi?i han 8 slot/ngay.");
        }
        else
        {
            if (await CountActiveBookings(userId, null, date) >= 4)
                throw new ConflictException("Ban da dat gi?i han 4 slot/ngay.");
        }

        var total = BookingPricing.SlotTotal(court, slot, date, request.PlayerCount, _pricing);

        Booking? booking = null;
        await Uow.RunInTransactionAsync(async () =>
        {
            await LockCourtDateAsync(court.Id, date);
            await Uow.LockPointsUserAsync(userId);
            var dailyLimit = request.GroupId.HasValue ? 8 : 4;
            if (await CountActiveBookings(userId, request.GroupId, date) >= dailyLimit)
                throw new ConflictException("Đã đạt giới hạn đặt sân trong ngày.");
            if (await GetCourtDateStatus(court.Id, date, slot) != CourtDateStatus.Open)
                throw new ConflictException("Sân bảo trì/đóng trong khung giờ này.");
            int voucherDiscount = 0;
            int pointsDiscount = 0;
            VoucherRedemption? voucherRedemption = null;

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                (voucherDiscount, voucherRedemption) =
                    await _vouchers.ReserveVoucherAsync(
                        request.VoucherCode, userId, total);
            }

            var (pointsRedeemedCount, calculatedPointsDiscount) = BookingPricing.Redeem(
                request.PointsToRedeem ?? 0, total - voucherDiscount);
            pointsDiscount = calculatedPointsDiscount;

            var finalAmount = Math.Max(0, total - voucherDiscount - pointsDiscount);

            var b = new Booking
            {
                BookingCode = GenerateCode(),
                IdempotencyKey = request.IdempotencyKey,
                RequestFingerprint = BookingRequestIdentity.Fingerprint(request, userId),
                UserId = userId,
                GroupId = request.GroupId,
                CourtId = court.Id,
                SlotId = slot.Id,
                BranchId = court.BranchId,
                Date = date,
                PlayerCount = Math.Max(1, request.PlayerCount),
                Status = BookingStatus.Pending,
                TotalAmount = total,
                DiscountAmount = voucherDiscount + pointsDiscount,
                PayableAmount = finalAmount,
                PointsRedeemed = pointsRedeemedCount,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Note = request.Note
            };

            // Legacy slot rows may overlap even when their IDs differ.
            var activeBookings = await Repo.GetByConditionAsync(x => x.CourtId == court.Id && x.Date == date
                && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed));
            var activeSlotIds = activeBookings.Select(x => x.SlotId).Distinct().ToList();
            var overlaps = await Repository<TimeSlot>().CountByConditionAsync(x => activeSlotIds.Contains(x.Id)
                && x.StartTime < slot.EndTime && slot.StartTime < x.EndTime);
            if (overlaps > 0)
                throw new ConflictException("SLOT_CONFLICT: sân đã có đơn giao với khung giờ này.");

            await Repo.AddAsync(b);
            var user = await Repository<User>().GetByIdAsync(userId);
            await Repository<BookingPlayer>().AddAsync(new BookingPlayer
            {
                UserId = userId,
                IsCreator = true,
                Booking = b,
                User = user
            });

            try { await SaveAsync(); }
            catch (UniqueConstraintException ex)
            {
                if (ex.ConstraintName == "ux_court_slot_active")
                    throw new ConflictException(
                        "SLOT_CONFLICT: san nay gio vay vua duoc dat, hay chon khung khac.");
                throw;
            }

            // Patch cross-links now that both rows have primary keys.
            if (voucherRedemption is not null)
            {
                voucherRedemption.BookingId = b.Id;
                b.VoucherRedemptionId = voucherRedemption.Id;
            }

            if (b.PointsRedeemed > 0)
                await _points.RedeemAsync(userId, b.Id, b.PointsRedeemed, b.BookingCode);

            if (voucherRedemption is not null || b.PointsRedeemed > 0)
                await SaveAsync();

            booking = b;
        });

        return booking!;
    }

    private async Task<int> CountActiveBookings(int userId, int? groupId, DateOnly date)
    {
        var u = userId; var g = groupId;
        return await Repo.CountByConditionAsync(b =>
            b.Date == date && b.UserId == u &&
            ((b.GroupId == null && g == null) || b.GroupId == g) &&
            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
    }

    private async Task<CourtDateStatus> GetCourtDateStatus(int courtId, DateOnly date, TimeSlot slot)
    {
        var rows = await Repository<CourtDateStatusEntry>().GetByConditionAsync(
            r => r.CourtId == courtId && r.Date == date);
        var row = rows.FirstOrDefault();
        return row?.Blocks(slot.StartTime, slot.EndTime) == true ? row.Status : CourtDateStatus.Open;
    }

    private static bool IsWeekend(DateOnly d) =>
        d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static DateOnly ParseDate(string s) =>
        DateOnly.TryParse(s, out var d) ? d : throw new BadRequestException("Ngay khong hop le.");

    private static string GenerateCode() =>
        "BD-" + DateTime.UtcNow.ToString("yyMMdd") + "-" +
        Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
