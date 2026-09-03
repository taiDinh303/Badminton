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
        }
    }
}
