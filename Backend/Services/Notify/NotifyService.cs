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
            Badminton.Core.Security.ICurrentUser currentUser,
            IOptions<NotifySettings> settings,
            IEnumerable<INotifyChannel> channels,
            Badminton.Core.Domain.IMetricsReporter? metrics = null)
            : base(uow, currentUser)
        {
            _settings = settings;
            _channels = channels.ToList();
            _metrics = metrics;
        }

        private IGenericRepository<Notification> Repo => Repository<Notification>();

        public async Task<IReadOnlyList<Notification>> EnqueueAndSendAsync(
            NotifyEvent evt, int userId, int? bookingId, string body)
        {
            var queued = await EnqueueAsync(evt, userId, bookingId, body);
            var results = new List<Notification>();
            foreach (var row in queued) results.Add(await DeliverAsync(row.Id, row.LedgerKey));
            return results;
        }

        private async Task<IReadOnlyList<Notification>> EnqueueAsync(NotifyEvent evt, int userId, int? bookingId, string body)
        {
            var user = await Repository<User>().GetByIdAsync(userId);
            if (user is null) return Array.Empty<Notification>();
            var results = new List<Notification>();
            foreach (var channel in _channels.Where(c => _settings.Value.ChannelList.Contains(c.Name)))
            {
                var key = $"{evt}:{bookingId ?? userId}:{channel.Name}";
                Notification queued = null!;
                // Persist first so a later retry can recover after a transport failure.
                await Uow.RunInTransactionAsync(async () =>
                {
                    await Uow.LockNotificationAsync(key);
                    queued = (await Repo.GetByConditionAsync(n => n.LedgerKey == key)).SingleOrDefault()!;
                    if (queued is not null) return;
                    queued = new Notification { UserId = userId, BookingId = bookingId, Event = evt,
                        LedgerKey = key, Channel = channel.Name, Body = body, Status = NotifyStatus.Pending, CreatedAt = DateTime.UtcNow };
                    await Repo.AddAsync(queued);
                    await SaveAsync();
                });
                results.Add(queued);
            }
            return results;
        }

        public async Task<int> RetryPendingAsync()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-2);
            var (pending, _) = await Repo.GetPagedAsync(n => n.DeletedAt == null
                && (n.Status == NotifyStatus.Pending || n.Status == NotifyStatus.Failed)
                && (n.UpdatedAt ?? n.CreatedAt) <= cutoff,
                n => n.UpdatedAt ?? n.CreatedAt, OrderDirection.Asc, new Page(1, 5));
            var delivered = 0;
            foreach (var row in pending)
                if ((await DeliverAsync(row.Id, row.LedgerKey)).Status == NotifyStatus.Delivered) delivered++;
            return delivered;
        }

        private async Task<Notification> DeliverAsync(int id, string key)
        {
            Notification result = null!;
            await Uow.RunInTransactionAsync(async () =>
            {
                await Uow.LockNotificationAsync(key);
                // A fresh snapshot is required even when this scope originally queued the row.
                var snapshot = (await Repo.GetByConditionAsync(n => n.Id == id)).Single();
                result = snapshot;
                if (snapshot.Status is NotifyStatus.Delivered or NotifyStatus.Skipped) return;
                var channel = _channels.SingleOrDefault(c => c.Name == snapshot.Channel
                    && _settings.Value.ChannelList.Contains(c.Name));
                if (channel is null) return; // retain pending rows while a channel is disabled
                var row = (await Repo.GetByIdAsync(id))!;
                var user = await Repository<User>().GetByIdAsync(snapshot.UserId);
                row.UpdatedAt = DateTime.UtcNow;
                if (!channel.IsConfigured || user is null) row.Status = NotifyStatus.Skipped;
                else
                {
                    try { await channel.SendAsync(user, snapshot.Event, snapshot.Body ?? ""); row.Status = NotifyStatus.Delivered; row.SentAt = DateTime.UtcNow; }
                    catch { row.Status = NotifyStatus.Failed; }
                }
                try { _metrics?.CountNotify(snapshot.Event.ToString().ToLowerInvariant(), channel.Name, row.Status.ToString().ToLowerInvariant()); }
                catch { /* Transport delivery must not depend on metrics. */ }
                Repo.Update(row);
                await SaveAsync();
                result = row;
            });
            return result;
        }

        public async Task QueueBookingEventAsync(NotifyEvent evt, int bookingId)
        {
            var b = await Repository<Booking>().GetByIdAsync(bookingId);
            if (b is null) return;
            var body = evt switch {
                NotifyEvent.BookingConfirmed => $"Đặt sân {b.BookingCode} đã được xác nhận thành công. Khung giờ {b.Date:dd/MM}. Cảm ơn bạn!",
                NotifyEvent.BookingExpired => $"Đặt sân {b.BookingCode} đã bị hủy do không thanh toán trong 10 phút. Vị trí giành cho bạn đã được mở lại.",
                NotifyEvent.NoShowRecorded => $"Ghi nhận bạn vắng mặt (no-show) với đơn {b.BookingCode}. Từ 3 lần trong tháng, bạn sẽ bị hạn chế đặt trong tháng kế tiếp.",
                _ => throw new ArgumentOutOfRangeException(nameof(evt))
            };
            await EnqueueAsync(evt, b.UserId, bookingId, body);
        }

        public async Task QueueWaitlistPromotedAsync(int waitlistEntryId, int bookingId)
        {
            var w = await Repository<WaitlistEntry>().GetByIdAsync(waitlistEntryId);
            var b = await Repository<Booking>().GetByIdAsync(bookingId);
            if (w is not null) await EnqueueAsync(NotifyEvent.WaitlistPromoted, w.UserId, bookingId,
                $"Mong đợi! Vị trí {w.Date:dd/MM} đã có chỗ cho bạn. Đã tạo đơn {b?.BookingCode ?? ""} — vui lòng thanh toán trong 10 phút.");
        }

        // ── Business-event helpers (Vietnamese copy) ─────────

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
