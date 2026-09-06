using Contract.Repositories.Entity;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context
{
    public class BadmintonBookingDbContext : DbContext
    {
        public BadmintonBookingDbContext(
            DbContextOptions<BadmintonBookingDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Role> Roles => Set<Role>();

        public DbSet<VerificationCode> VerificationCodes { get; set; }

        public DbSet<Court> Courts => Set<Court>();

        public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

        public DbSet<Booking> Bookings => Set<Booking>();

        public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();

        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");

                entity.HasKey(x => x.RoleId);

                entity.Property(x => x.Name)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(255);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreatedAt)
                    .HasColumnType("datetime2(0)")
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasIndex(x => x.Name)
                    .IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");

                entity.HasKey(x => x.UserId);

                entity.Property(x => x.FullName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Email)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.PhoneNumber)
                    .HasMaxLength(15)
                    .IsUnicode(false);

                entity.Property(x => x.PasswordHash)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.AvatarUrl)
                    .HasMaxLength(500);

                entity.Property(x => x.Gender)
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.CreatedAt)
                    .HasColumnType("datetime2(0)")
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.UpdatedAt)
                    .HasColumnType("datetime2(0)");

                entity.HasIndex(x => x.Email)
                    .IsUnique();

                entity.HasIndex(x => x.RoleId);

                entity.HasOne(x => x.Role)
                    .WithMany(x => x.Users)
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<VerificationCode>(entity =>
            {
                entity.HasKey(x => x.VerificationCodeId);

                entity.Property(x => x.CodeHash)
                    .IsRequired();

                entity.Property(x => x.Type)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.ToTable("Bookings");

                entity.HasKey(x => x.BookingId);

                entity.Property(x => x.BookingCode)
                    .HasMaxLength(30)
                    .IsRequired()
                    .IsUnicode(false);

                entity.Property(x => x.BookingDate)
                    .HasColumnType("date")
                    .IsRequired();

                entity.Property(x => x.TotalAmount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired()
                    .IsUnicode(false);

                entity.Property(x => x.Note)
                    .HasMaxLength(500);

                entity.Property(x => x.CreatedAt)
                    .HasColumnType("datetime2(0)")
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.UpdatedAt)
                    .HasColumnType("datetime2(0)");

                entity.HasIndex(x => x.BookingCode)
                    .IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(x => x.BookingDetails)
                    .WithOne(x => x.Booking)
                    .HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BookingDetail>(entity =>
            {
                entity.ToTable("BookingDetails");

                entity.HasKey(x => x.BookingDetailId);

                entity.Property(x => x.BookingDate)
                    .HasColumnType("date")
                    .IsRequired();

                entity.Property(x => x.Price)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired()
                    .IsUnicode(false);

                entity.HasOne(x => x.Booking)
                    .WithMany(x => x.BookingDetails)
                    .HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Court)
                    .WithMany(x => x.BookingDetails)
                    .HasForeignKey(x => x.CourtId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.TimeSlot)
                    .WithMany(x => x.BookingDetails)
                    .HasForeignKey(x => x.TimeSlotId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.BookingId);
            });

            modelBuilder.Entity<Court>(entity =>
            {
                entity.ToTable("Courts");

                entity.HasKey(x => x.CourtId);

                entity.Property(x => x.CourtCode)
                    .HasMaxLength(20)
                    .IsRequired()
                    .IsUnicode(false);

                entity.Property(x => x.CourtName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(500);

                entity.Property(x => x.Location)
                    .HasMaxLength(255);

                entity.Property(x => x.PricePerHour)
                    .HasColumnType("decimal(18,2)");

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired()
                    .IsUnicode(false);

                entity.Property(x => x.ImageUrl)
                    .HasMaxLength(500);

                entity.Property(x => x.CreatedAt)
                    .HasColumnType("datetime2(0)")
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.UpdatedAt)
                    .HasColumnType("datetime2(0)");

                entity.HasIndex(x => x.CourtCode)
                    .IsUnique();
            });

            modelBuilder.Entity<TimeSlot>(entity =>
            {
                entity.ToTable("TimeSlots");

                entity.HasKey(x => x.TimeSlotId);

                entity.Property(x => x.StartTime)
                    .HasColumnType("time(0)")
                    .IsRequired();

                entity.Property(x => x.EndTime)
                    .HasColumnType("time(0)")
                    .IsRequired();

                entity.HasIndex(x => new
                {
                    x.StartTime,
                    x.EndTime
                }).IsUnique();
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("Payments");

                entity.HasKey(x => x.PaymentId);

                entity.Property(x => x.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(x => x.PaymentMethod)
                    .HasMaxLength(30)
                    .IsUnicode(false);

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(x => x.TransactionCode)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(x => x.CreatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasOne(x => x.Booking)
                    .WithOne(x => x.Payment)
                    .HasForeignKey<Payment>(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => x.BookingId)
                    .IsUnique();
            });

            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.ToTable("BankAccounts");

                entity.HasKey(x => x.BankAccountId);

                entity.Property(x => x.BankName)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .IsRequired();

                entity.Property(x => x.AccountNumber)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .IsRequired();

                entity.Property(x => x.AccountHolder)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.AccountType)
                    .HasMaxLength(30)
                    .IsUnicode(false)
                    .IsRequired();

                entity.Property(x => x.IsDefault)
                    .HasDefaultValue(false);

                entity.Property(x => x.CreatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
