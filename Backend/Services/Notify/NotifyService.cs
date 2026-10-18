using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Notify;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.Extensions.Options;

namespace Services
{
    public sealed class NotifyService : BaseService, INotifyService
    {
        private readonly IOptions<NotifySettings> _settings;
        private readonly IReadOnlyList<INotifyChannel> _channels;
        private readonly Badminton.Core.Domain.IMetricsReporter? _metrics;

        public NotifyService(IUnitOfWork uow,
            Microsoft.AspNetCore.Http.IHttpContextAccessor http,
            IOptions<NotifySettings> settings,
            IEnumerable<INotifyChannel> channels,
            Badminton.Core.Domain.IMetricsReporter? metrics = null)
            : base(uow, http)
        {
            _settings = settings;
            _channels = channels.ToList();
            _metrics = metrics;
        }

        private IGenericRepository<Notification> Repo => Repository<Notification>();

        public async Task<IReadOnlyList<Notification>> EnqueueAndSendAsync(
            NotifyEvent evt, int userId, int? bookingId, string body)
        {
            var user = await Repository<User>().GetByIdAsync(userId);
            if (user is null) return Array.Empty<Notification>();

            var active = _settings.Value.ChannelList;
            var created = new List<Notification>();

            foreach (var ch in _channels)
            {
                if (!active.Contains(ch.Name)) continue; // not an enabled channel

                var key = $"{evt}:{bookingId ?? userId}:{ch.Name}";
                if (await Repo.CountByConditionAsync(n => n.LedgerKey == key) > 0)
                {
                    // Idempotent replay: keep the existing row, do not re-send.
                    created.Add((await Repo.GetByConditionAsync(n => n.LedgerKey == key))
                        .FirstOrDefault()!);
                    continue;
                }

                var row = new Notification
                {
                    UserId = userId,
                    BookingId = bookingId,
                    Event = evt,
                    LedgerKey = key,
                    Channel = ch.Name,
                    Status = NotifyStatus.Pending,
                    Body = body,
                    CreatedAt = DateTime.UtcNow
                };
                _ = Repo.AddAsync(row);
                await SaveAsync();

                if (!ch.IsConfigured)
                {
                    row.Status = NotifyStatus.Skipped; // enabled but no endpoint
                }
                else
                {
                    try
                    {
                        await ch.SendAsync(user, evt, body);
                        row.Status = NotifyStatus.Delivered;
                        row.SentAt = DateTime.UtcNow;
                    }
                    catch
                    {
                        row.Status = NotifyStatus.Failed;
                    }
                }
                try
                {
                    _metrics?.CountNotify(
                        evt.ToString().ToLowerInvariant(),
                        ch.Name,
                        row.Status.ToString().ToLowerInvariant());
                }
                catch { /* metrics must never break the notify fan-out */ }
                Repo.Update(row);
                await SaveAsync();
                created.Add(row);
            }

            return created;
        }

        // ── Business-event helpers (Vietnamese copy, docs 01 §7) ─────────

        public async Task NotifyBookingConfirmedAsync(int bookingId)
        {
            var b = await Repository<Booking>().GetByIdAsync(bookingId);
            if (b is null) return;
            await EnqueueAndSendAsync(NotifyEvent.BookingConfirmed, b.UserId, bookingId,
                $"Đặt sân {b.BookingCode} đã được xác nhận thành công. " +
                $"Khung giờ {b.Date:dd/MM}. Cảm ơn bạn!");
        }

        public async Task NotifyBookingExpiredAsync(int bookingId)
        {
            var b = await Repository<Booking>().GetByIdAsync(bookingId);
            if (b is null) return;
            await EnqueueAndSendAsync(NotifyEvent.BookingExpired, b.UserId, bookingId,
                $"Đặt sân {b.BookingCode} đã bị hủy do không thanh toán trong 10 phút. " +
                $"Vị trí giành cho bạn đã được mở lại.");
        }

        public async Task NotifyNoShowAsync(int bookingId)
        {
            var b = await Repository<Booking>().GetByIdAsync(bookingId);
            if (b is null) return;
            await EnqueueAndSendAsync(NotifyEvent.NoShowRecorded, b.UserId, bookingId,
                $"Ghi nhận bạn vắng mặt (no-show) với đơn {b.BookingCode}. " +
                $"Từ 3 lần trong tháng, bạn sẽ bị hạn chế đặt trong tháng kế tiếp.");
        }

        public async Task NotifyWaitlistPromotedAsync(int waitlistEntryId, int bookingId)
        {
            var w = await Repository<WaitlistEntry>().GetByIdAsync(waitlistEntryId);
            if (w is null) return;
            var b = await Repository<Booking>().GetByIdAsync(bookingId);
            await EnqueueAndSendAsync(NotifyEvent.WaitlistPromoted, w.UserId, bookingId,
                $"Mong đợi! Vị trí {w.Date:dd/MM} đã có chỗ cho bạn. " +
                $"Đã tạo đơn {b?.BookingCode ?? ""} — vui lòng thanh toán trong 10 phút.");
        }
    }
}
