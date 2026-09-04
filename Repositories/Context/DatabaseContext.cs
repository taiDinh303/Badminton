//using BadmintonBooking.Contract.Repositories.BookingEntity;
//using Contract.Repositories.BookingEntity;
//using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore;

//namespace BadmintonBooking.Repositories.Context;

//public class DatabaseContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid, ApplicationUserClaim, ApplicationUserRole, ApplicationUserLogin, ApplicationRoleClaim, ApplicationUserToken>
//{
//    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

//    // Identity
//    public virtual DbSet<ApplicationUser> ApplicationUser => Set<ApplicationUser>();
//    public virtual DbSet<ApplicationRole> ApplicationRole => Set<ApplicationRole>();
//    public virtual DbSet<ApplicationUserClaim> ApplicationUserClaim => Set<ApplicationUserClaim>();
//    public virtual DbSet<ApplicationUserRole> ApplicationUserRole => Set<ApplicationUserRole>();
//    public virtual DbSet<ApplicationUserLogin> ApplicationUserLogin => Set<ApplicationUserLogin>();
//    public virtual DbSet<ApplicationRoleClaim> ApplicationRoleClaim => Set<ApplicationRoleClaim>();
//    public virtual DbSet<ApplicationUserToken> ApplicationUserToken => Set<ApplicationUserToken>();

//    // Domain
//    public virtual DbSet<UserInfo> UserInfos => Set<UserInfo>();
//    public virtual DbSet<BankAccount> BankAccounts => Set<BankAccount>();
//    public virtual DbSet<Booking> Bookings => Set<Booking>();
//    public virtual DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();
//    public virtual DbSet<Branch> Branches => Set<Branch>();
//    public virtual DbSet<CalendarType> CalendarTypes => Set<CalendarType>();
//    public virtual DbSet<Court> Courts => Set<Court>();
//    public virtual DbSet<CourtType> CourtTypes => Set<CourtType>();
//    public virtual DbSet<Payment> Payments => Set<Payment>();
//    public virtual DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

//    protected override void OnModelCreating(ModelBuilder builder)
//    {
//        base.OnModelCreating(builder);

//        // ===== UserInfo =====
//        builder.Entity<UserInfo>(entity =>
//        {
//            entity.HasKey(x => x.Id);
//        });

//        // ===== BankAccount =====
//        builder.Entity<BankAccount>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.BankName)
//                  .IsRequired()
//                  .HasMaxLength(100);

//            entity.Property(x => x.AccountNumber)
//                  .IsRequired()
//                  .HasMaxLength(50);

//            entity.Property(x => x.AccountHolder)
//                  .IsRequired()
//                  .HasMaxLength(150);

//            entity.Property(x => x.QRCodeUrl)
//                  .HasMaxLength(500);
//        });

//        // ===== Booking =====
//        builder.Entity<Booking>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Price)
//                  .HasColumnType("decimal(18,2)");

//            entity.Property(x => x.Status)
//                  .IsRequired()
//                  .HasMaxLength(50);

//            entity.Property(x => x.UserInfoId)
//                  .IsRequired();

//            entity.Property(x => x.UserName)
//                  .IsRequired()
//                  .HasMaxLength(150);

//            entity.Property(x => x.PhoneNumber)
//                  .IsRequired()
//                  .HasMaxLength(20);

//            entity.Property(x => x.BankAccountID)
//                  .IsRequired();

//            entity.HasOne(x => x.UserInfo)
//                  .WithMany()
//                  .HasForeignKey(x => x.UserInfoId)
//                  .OnDelete(DeleteBehavior.Restrict);

//            entity.HasOne(x => x.BankAccount)
//                  .WithMany(x => x.Bookings)
//                  .HasForeignKey(x => x.BankAccountID)
//                  .OnDelete(DeleteBehavior.Restrict);
//        });

//        // ===== BookingDetail =====
//        builder.Entity<BookingDetail>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Price)
//                  .HasColumnType("decimal(18,2)");

//            entity.Property(x => x.Status)
//                  .IsRequired()
//                  .HasMaxLength(50);

//            entity.Property(x => x.Note)
//                  .HasMaxLength(500);

//            entity.HasOne(x => x.Booking)
//                  .WithMany(x => x.BookingDetails)
//                  .HasForeignKey(x => x.BookingId)
//                  .OnDelete(DeleteBehavior.Cascade);

//            entity.HasOne(x => x.Court)
//                  .WithMany(x => x.BookingDetails)
//                  .HasForeignKey(x => x.CourtId)
//                  .OnDelete(DeleteBehavior.Restrict);

//            entity.HasOne(x => x.TimeSlot)
//                  .WithMany(x => x.BookingDetails)
//                  .HasForeignKey(x => x.TimeSlotId)
//                  .OnDelete(DeleteBehavior.Restrict);
//        });

//        // ===== Branch =====
//        builder.Entity<Branch>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Name)
//                  .IsRequired()
//                  .HasMaxLength(150);

//            entity.Property(x => x.Address)
//                  .IsRequired()
//                  .HasMaxLength(300);

//            entity.Property(x => x.PhoneNumber)
//                  .IsRequired()
//                  .HasMaxLength(20);

//            entity.Property(x => x.Description)
//                  .HasMaxLength(500);
//        });

//        // ===== CalendarType =====
//        builder.Entity<CalendarType>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Name)
//                  .IsRequired()
//                  .HasMaxLength(100);

//            entity.Property(x => x.Description)
//                  .HasMaxLength(500);
//        });

//        // ===== Court =====
//        builder.Entity<Court>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Name)
//                  .IsRequired()
//                  .HasMaxLength(100);

//            entity.Property(x => x.Description)
//                  .HasMaxLength(500);

//            entity.HasOne(x => x.CourtType)
//                  .WithMany(x => x.Courts)
//                  .HasForeignKey(x => x.CourtTypeId)
//                  .OnDelete(DeleteBehavior.Restrict);
//        });

//        // ===== CourtType =====
//        builder.Entity<CourtType>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Name)
//                  .IsRequired()
//                  .HasMaxLength(100);

//            entity.Property(x => x.Description)
//                  .HasMaxLength(500);
//        });

//        // ===== Payment =====
//        builder.Entity<Payment>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.Property(x => x.Amount)
//                  .HasColumnType("decimal(18,2)");

//            entity.Property(x => x.PaymentMethod)
//                  .IsRequired()
//                  .HasMaxLength(50);

//            entity.Property(x => x.Status)
//                  .IsRequired()
//                  .HasMaxLength(50);

//            entity.Property(x => x.TransactionCode)
//                  .HasMaxLength(100);

//            entity.HasOne(x => x.Booking)
//                  .WithMany(x => x.Payments)
//                  .HasForeignKey(x => x.BookingId)
//                  .OnDelete(DeleteBehavior.Cascade);
//        });

//        // ===== TimeSlot =====
//        builder.Entity<TimeSlot>(entity =>
//        {
//            entity.HasKey(x => x.Id);

//            entity.HasOne(x => x.CalendarType)
//                  .WithMany(x => x.TimeSlots)
//                  .HasForeignKey(x => x.CalendarTypeId)
//                  .OnDelete(DeleteBehavior.Restrict);
//        });
//    }
//}