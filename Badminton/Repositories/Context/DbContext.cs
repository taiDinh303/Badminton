using Contract.Repositories.Entity;
using M.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Repositories.Context
{
    /// <summary>
    /// EF Core context for the whole booking model. The SERVER layer wires up
    /// the SQL Server provider; this class only declares the mapping.
    /// </summary>
    public class BadmintonBookingDbContext : DbContext
    {
        public BadmintonBookingDbContext(DbContextOptions<BadmintonBookingDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<CourtType> CourtTypes => Set<CourtType>();
        public DbSet<Court> Courts => Set<Court>();
        public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
        public DbSet<CheckIn> CheckIns => Set<CheckIn>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Every int primary key is SQL IDENTITY: mark them ValueGeneratedOnAdd()
            // so EF lets the DB assign the value (avoids the IDENTITY_INSERT error
            // when the seed or a service sets a non-zero id).
            // ---- Enums stored as string (enum names), so the DB stays readable
            //      and matches the SQL CHECK constraints exactly. ----

            // ---- Roles / Users (pre-existing) ----
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Roles");
                e.HasKey(x => x.RoleId);
                e.Property(x => x.RoleId).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(50).IsRequired();
                e.Property(x => x.Description).HasMaxLength(255);
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.UserId);
                e.Property(x => x.UserId).ValueGeneratedOnAdd();
                e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Email).HasMaxLength(150).IsRequired();
                e.Property(x => x.PhoneNumber).HasMaxLength(15).IsUnicode(false);
                e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
                e.Property(x => x.AvatarUrl).HasMaxLength(500);
                e.Property(x => x.Gender).HasMaxLength(10).IsUnicode(false);
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
                e.HasIndex(x => x.Email).IsUnique();
                e.HasIndex(x => x.RoleId);
                e.HasOne(x => x.Role).WithMany(x => x.Users)
                    .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            });

            // ---- CourtType ----
            modelBuilder.Entity<CourtType>(e =>
            {
                e.ToTable("CourtTypes");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(50).IsRequired();
                e.Property(x => x.Description).HasMaxLength(255);
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.Name).IsUnique();
            });

            // ---- Court ----
            modelBuilder.Entity<Court>(e =>
            {
                e.ToTable("Courts");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.CourtCode).HasMaxLength(20).IsRequired();
                e.Property(x => x.CourtName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.Location).HasMaxLength(255);
                e.Property(x => x.PricePerHour).HasPrecision(18, 2);
                e.Property(x => x.Status)
                    .HasConversion(
                        s => s.ToString(),
                        s => Enum.Parse<CourtStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(CourtStatus.Available);
                e.Property(x => x.ImageUrl).HasMaxLength(500);
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
                e.HasIndex(x => x.CourtCode).IsUnique();
                e.HasIndex(x => x.CourtTypeId);
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.CourtType)
                    .WithMany()
                    .HasForeignKey(x => x.CourtTypeId).OnDelete(DeleteBehavior.Restrict);
            });

            // ---- TimeSlot ----
            modelBuilder.Entity<TimeSlot>(e =>
            {
                e.ToTable("TimeSlots");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.HasIndex(x => new { x.StartTime, x.EndTime }).IsUnique();
                e.Property(x => x.IsActive).HasDefaultValue(true);
            });

            // ---- Booking ----
            modelBuilder.Entity<Booking>(e =>
            {
                e.ToTable("Bookings");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.BookingCode).HasMaxLength(30).IsRequired();
                e.Property(x => x.TotalAmount).HasPrecision(18, 2);
                e.Property(x => x.Status)
                    .HasConversion(
                        s => s.ToString(),
                        s => Enum.Parse<BookingStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(BookingStatus.Pending);
                e.Property(x => x.Note).HasMaxLength(500);
                e.Property(x => x.AppliedFeePolicy).HasMaxLength(50);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
                e.HasIndex(x => x.BookingCode).IsUnique();
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.BookingDate);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => new { x.BookingDate, x.Status });
                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            // ---- BookingDetail ----
            modelBuilder.Entity<BookingDetail>(e =>
            {
                e.ToTable("BookingDetails");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Price).HasPrecision(18, 2);
                e.Property(x => x.Status)
                    .HasConversion(
                        s => s.ToString(),
                        s => Enum.Parse<BookingDetailStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(BookingDetailStatus.Reserved);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.BookingId);
                e.HasIndex(x => new { x.CourtId, x.BookingDate });
                e.HasIndex(x => x.TimeSlotId);
                e.HasOne(x => x.Booking)
                    .WithMany(b => b.Details)
                    .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Court)
                    .WithMany()
                    .HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.TimeSlot)
                    .WithMany()
                    .HasForeignKey(x => x.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
                // A slot may not be reserved twice (active rows only).
                e.HasIndex(x => new { x.CourtId, x.BookingDate, x.TimeSlotId }).IsUnique();
            });

            // ---- Payment ----
            modelBuilder.Entity<Payment>(e =>
            {
                e.ToTable("Payments");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Method)
                    .HasConversion(
                        s => s.ToString(),
                        s => Enum.Parse<PaymentMethod>(s))
                    .HasMaxLength(30);
                e.Property(x => x.TransactionCode).HasMaxLength(100);
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.Property(x => x.Status)
                    .HasConversion(
                        s => s.ToString(),
                        s => Enum.Parse<PaymentStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(PaymentStatus.Pending);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.BookingId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.TransactionCode).IsUnique();
                e.HasOne(x => x.Booking)
                    .WithOne(b => b.Payment)
                    .HasForeignKey<Payment>(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ---- BankAccount ----
            modelBuilder.Entity<BankAccount>(e =>
            {
                e.ToTable("BankAccounts");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.BankName).HasMaxLength(100).IsRequired();
                e.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired();
                e.Property(x => x.AccountHolderName).HasMaxLength(150).IsRequired();
                e.Property(x => x.IsDefault).HasDefaultValue(false);
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.UserId);
                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            });

            // ---- CheckIn ----
            modelBuilder.Entity<CheckIn>(e =>
            {
                e.ToTable("CheckIns");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Status)
                    .HasConversion(
                        s => s.ToString(),
                        s => Enum.Parse<CheckInStatus>(s))
                    .HasMaxLength(20).HasDefaultValue(CheckInStatus.Pending);
                e.Property(x => x.Note).HasMaxLength(500);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.BookingId).IsUnique();
                e.HasIndex(x => x.UserId);
                e.HasOne(x => x.Booking)
                    .WithMany()
                    .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Review ----
            modelBuilder.Entity<Review>(e =>
            {
                e.ToTable("Reviews");
                e.HasKey(x => x.Id);
                e.Property(x => x.Comment).HasMaxLength(1000);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => new { x.UserId, x.BookingId, x.CourtId }).IsUnique();
                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Court)
                    .WithMany()
                    .HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Booking)
                    .WithMany()
                    .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Notification ----
            modelBuilder.Entity<Notification>(e =>
            {
                e.ToTable("Notifications");
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.Property(x => x.Message).HasMaxLength(1000).IsRequired();
                e.Property(x => x.Type).HasMaxLength(30);
                e.Property(x => x.IsRead).HasDefaultValue(false);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
