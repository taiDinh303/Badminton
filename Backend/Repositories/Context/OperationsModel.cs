using Contract.Repositories.Entity;
using Microsoft.EntityFrameworkCore;
namespace Repositories.Context;

internal static class OperationsModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<BookingBatch>(e =>
        {
            e.ToTable("BookingBatches");
            e.HasIndex(x => new { x.ActorId, x.RequestKey }).IsUnique();
            e.Property(x => x.RequestKey).HasMaxLength(60);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
        });
        mb.Entity<BookingCase>(e =>
        {
            e.ToTable("BookingCases");
            e.HasIndex(x => new { x.BookingId, x.Status });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<InboxMessage>(e =>
        {
            e.ToTable("InboxMessages");
            e.HasIndex(x => new { x.UserId, x.EventKey }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.Property(x => x.EventKey).HasMaxLength(150);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<NotificationPreference>(e =>
        {
            e.ToTable("NotificationPreferences");
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<DisruptionContact>(e =>
        {
            e.ToTable("DisruptionContacts");
            e.HasIndex(x => new { x.BookingId, x.IncidentKey }).IsUnique();
            e.HasIndex(x => new { x.BranchId, x.ContactedAt });
            e.Property(x => x.IncidentKey).HasMaxLength(60);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<StaffShift>(e =>
        {
            e.ToTable("StaffShifts");
            e.HasIndex(x => new { x.BranchId, x.StartsAtUtc });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<HandoverItem>(e =>
        {
            e.ToTable("HandoverItems");
            e.HasIndex(x => new { x.BranchId, x.ClosedAt });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<PrivacyRequest>(e =>
        {
            e.ToTable("PrivacyRequests");
            e.HasIndex(x => new { x.UserId, x.Status });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<ReviewReport>(e =>
        {
            e.ToTable("ReviewReports");
            e.HasIndex(x => new { x.ReviewId, x.UserId }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Review>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<ReviewReply>(e =>
        {
            e.ToTable("ReviewReplies");
            e.HasIndex(x => x.ReviewId).IsUnique();
            e.HasOne<Review>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<StoredMedia>(e =>
        {
            e.ToTable("StoredMedia");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(80);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Review>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<CourtFavorite>(e =>
        {
            e.ToTable("CourtFavorites");
            e.HasIndex(x => new { x.UserId, x.CourtId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<GroupRsvp>(e =>
        {
            e.ToTable("GroupRsvps");
            e.HasIndex(x => new { x.BookingId, x.UserId }).IsUnique();
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<BookingHistory>(e =>
        {
            e.ToTable("BookingHistory");
            e.HasIndex(x => new { x.BookingId, x.CreatedAt });
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        Type[] types = [typeof(BookingCase), typeof(InboxMessage), typeof(NotificationPreference), typeof(DisruptionContact),
            typeof(StaffShift), typeof(HandoverItem), typeof(PrivacyRequest), typeof(ReviewReport), typeof(ReviewReply),
            typeof(StoredMedia), typeof(CourtFavorite), typeof(GroupRsvp), typeof(BookingHistory)];
        foreach (var type in types)
            foreach (var property in mb.Entity(type).Metadata.GetProperties().Where(p => p.ClrType == typeof(string)))
                if (property.GetMaxLength() is null)
                    property.SetMaxLength(2000);
        mb.Entity<Review>().Property(x => x.ModerationReason).HasMaxLength(1500);
        mb.Entity<Notification>().Property(x => x.LastError).HasMaxLength(200);
    }
}
