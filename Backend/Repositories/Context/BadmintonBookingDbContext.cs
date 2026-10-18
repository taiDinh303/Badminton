using Contract.Repositories.Entity;
using Badminton.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context
{
    public class BadmintonBookingDbContext : DbContext
    {
        public BadmintonBookingDbContext(DbContextOptions<BadmintonBookingDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Court> Courts => Set<Court>();
        public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
        public DbSet<CourtDateStatusEntry> CourtDateStatuses => Set<CourtDateStatusEntry>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<BookingPlayer> BookingPlayers => Set<BookingPlayer>();
        public DbSet<PlayGroup> PlayGroups => Set<PlayGroup>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Voucher> Vouchers => Set<Voucher>();
        public DbSet<VoucherRedemption> VoucherRedemptions => Set<VoucherRedemption>();
        public DbSet<PointLedger> PointLedger => Set<PointLedger>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // ---- Enums as strings (applied per property below) ----

            mb.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                e.Property(x => x.PhoneNumber).HasMaxLength(15).IsRequired();
                e.Property(x => x.Email).HasMaxLength(150);
                e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
                e.Property(x => x.AvatarUrl).HasMaxLength(500);
                e.Property(x => x.Role)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<Role>(s))
                    .HasMaxLength(20).HasDefaultValue(Role.Customer);
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<UserStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(UserStatus.Active);
                // Membership V2 (enum string + nullable date).
                e.Property(x => x.Tier)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<MembershipTier>(s))
                    .HasMaxLength(20).HasDefaultValue(MembershipTier.None);
                e.HasIndex(x => x.PhoneNumber).IsUnique();
                e.HasIndex(x => new { x.Role, x.Status });

                e.HasIndex(x => x.Role)
                    .HasFilter("[Status] = N'Active' AND [Role] = N'Admin'")
                    .IsUnique()
                    .HasDatabaseName("ux_active_admin");
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- Branches ----
            mb.Entity<Branch>(e =>
            {
                e.ToTable("Branches");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.Address).HasMaxLength(255);
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<BranchStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(BranchStatus.Active);
            });

            // ---- Courts ----
            mb.Entity<Court>(e =>
            {
                e.ToTable("Courts");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Code).HasMaxLength(20).IsRequired();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Surface)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<CourtSurface>(s))
                    .HasMaxLength(20).HasDefaultValue(CourtSurface.Sindalo);
                // int prices map to SQL Server int by default (integer VND/hour)
                e.Property(x => x.WeekendSurcharge).HasPrecision(10, 4);
                e.Property(x => x.Amenities).HasMaxLength(500);
                e.Property(x => x.ImageUrl).HasMaxLength(500);
                e.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
                e.HasIndex(x => x.BranchId);
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- TimeSlots ----
            mb.Entity<TimeSlot>(e =>
            {
                e.ToTable("TimeSlots");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Label).HasMaxLength(20).IsRequired();
                e.HasIndex(x => new { x.StartTime, x.EndTime }).IsUnique();
            });

            // ---- CourtDateStatusEntry (PK (court_id, date)) ----
            mb.Entity<CourtDateStatusEntry>(e =>
            {
                e.ToTable("CourtDateStatus");
                e.HasKey(x => new { x.CourtId, x.Date });
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<CourtDateStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(CourtDateStatus.Open);
                e.HasIndex(x => x.Date);
                e.HasOne(x => x.Court).WithMany()
                    .HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Cascade);
            });

            // ---- Bookings ----
            mb.Entity<Booking>(e =>
            {
                e.ToTable("Bookings");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.BookingCode).HasMaxLength(30).IsRequired();
                e.Property(x => x.IdempotencyKey).HasMaxLength(80);
                e.HasIndex(x => x.BookingCode).IsUnique();
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<BookingStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(BookingStatus.Pending);
                e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");

                // QR check-in token: unique among non-null values.
                e.Property(x => x.QrToken).HasMaxLength(64);
                e.HasIndex(x => x.QrToken).IsUnique().HasFilter("[QrToken] IS NOT NULL");

                // one slot may hold at most ONE active (pending/confirmed) booking.
                e.HasIndex(x => new { x.CourtId, x.Date, x.SlotId })
                    .IsUnique()
                    .HasFilter("[Status] IN (N'Pending', N'Confirmed')")
                    .HasDatabaseName("ux_court_slot_active");
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.Date);
                e.HasIndex(x => new { x.BranchId, x.Date });
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.SlotId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- BookingPlayers (PK (booking_id, user_id)) ----
            mb.Entity<BookingPlayer>(e =>
            {
                e.ToTable("BookingPlayers");
                e.HasKey(x => new { x.BookingId, x.UserId });
                e.HasOne(x => x.Booking).WithMany(b => b.Players)
                    .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- PlayGroups / GroupMembers ----
            mb.Entity<PlayGroup>(e =>
            {
                e.ToTable("PlayGroups");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            mb.Entity<GroupMember>(e =>
            {
                e.ToTable("GroupMembers");
                e.HasKey(x => new { x.GroupId, x.UserId });
                e.HasOne(x => x.Group).WithMany(g => g.Members)
                    .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Payments (refund = NEW record; gateway_ref unique) ----
            mb.Entity<Payment>(e =>
            {
                e.ToTable("Payments");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Method)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<PaymentMethod>(s))
                    .HasMaxLength(20);
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<PaymentStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(PaymentStatus.Pending);
                e.Property(x => x.GatewayRef).HasMaxLength(120);
                e.Property(x => x.RawResponse).HasMaxLength(2000);
                e.HasIndex(x => x.BookingId);
                e.HasIndex(x => x.GatewayRef).IsUnique().HasFilter("\"GatewayRef\" IS NOT NULL");
                e.HasIndex(x => new { x.Status, x.PaidAt });
                e.HasOne(x => x.Booking).WithMany(b => b.Payments)
                    .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Vouchers / Redemptions ----
            mb.Entity<Voucher>(e =>
            {
                e.ToTable("Vouchers");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Code).HasMaxLength(30).IsRequired();
                e.Property(x => x.Type)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<VoucherType>(s))
                    .HasMaxLength(10);
                e.HasIndex(x => x.Code).IsUnique();
            });

            mb.Entity<VoucherRedemption>(e =>
            {
                e.ToTable("VoucherRedemptions");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                // one customer uses one voucher once.
                e.HasIndex(x => new { x.VoucherId, x.UserId }).IsUnique();
                e.HasOne(x => x.Voucher).WithMany()
                    .HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- PointLedger (append-only, indexed by user) ----
            mb.Entity<PointLedger>(e =>
            {
                e.ToTable("PointLedger");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Reason).HasMaxLength(100);
                e.Property(x => x.LedgerKey).HasMaxLength(80);
                e.HasIndex(x => new { x.UserId, x.CreatedAt });

                e.HasIndex(x => x.LedgerKey)
                    .IsUnique()
                    .HasFilter("[LedgerKey] IS NOT NULL");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- AuditLogs ----
            mb.Entity<AuditLog>(e =>
            {
                e.ToTable("AuditLogs");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Action).HasMaxLength(60);
                e.Property(x => x.EntityType).HasMaxLength(40);
                e.Property(x => x.EntityId).HasMaxLength(40);
                e.Property(x => x.BeforeJson).HasMaxLength(2000);
                e.Property(x => x.AfterJson).HasMaxLength(2000);
                e.Property(x => x.Reason).HasMaxLength(500);
                e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
                e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- Reviews (one per booking, court avg is maintained) ----
            mb.Entity<Review>(e =>
            {
                e.ToTable("Reviews");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Comment).HasMaxLength(1000);
                e.Property(x => x.Images).HasMaxLength(500);
                e.HasIndex(x => x.BookingId).IsUnique();
                e.HasIndex(x => x.CourtId);
                e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Notifications (outbox; LedgerKey dedups one row per event) ----
            mb.Entity<Notification>(e =>
            {
                e.ToTable("Notifications");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Channel).HasMaxLength(20).HasDefaultValue("email");
                e.Property(x => x.ExternalRef).HasMaxLength(120);
                e.Property(x => x.Body).HasMaxLength(500);
                e.Property(x => x.LedgerKey).HasMaxLength(80).IsRequired();
                e.Property(x => x.Event)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<NotifyEvent>(s))
                    .HasMaxLength(20);
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<NotifyStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(NotifyStatus.Pending);
                e.HasIndex(x => x.LedgerKey).IsUnique();
                e.HasIndex(x => new { x.UserId, x.CreatedAt });
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- WaitlistEntries (waiting FIFO per slot) ----
            mb.Entity<WaitlistEntry>(e =>
            {
                e.ToTable("WaitlistEntries");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Note).HasMaxLength(200);
                e.Property(x => x.DeclineReason).HasMaxLength(200);
                e.Property(x => x.Status)
                    .HasConversion(s => s.ToString(), s => Enum.Parse<WaitlistStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(WaitlistStatus.Waiting);
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => new { x.CourtId, x.Date, x.SlotId, x.CreatedAt });
                // Only one waiting entry per (user, court, slot, date) — no double queue.
                e.HasIndex(x => new { x.UserId, x.CourtId, x.Date, x.SlotId })
                    .IsUnique()
                    .HasFilter("[Status] = N'Waiting'");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.SlotId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
