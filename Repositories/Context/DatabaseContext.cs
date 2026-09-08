using Contract.Repositories.Entity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context
{
    public class DatabaseContext : IdentityDbContext<ApplicationUser,
        ApplicationRole,
        Guid,
        ApplicationUserClaim,
        ApplicationUserRole,
        ApplicationUserLogin,
        ApplicationRoleClaim,
        ApplicationUserToken>
    {
        public DatabaseContext(
            DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        // Identity
        public virtual DbSet<ApplicationUser> ApplicationUser => Set<ApplicationUser>();
        public virtual DbSet<ApplicationRole> ApplicationRole => Set<ApplicationRole>();
        public virtual DbSet<ApplicationUserClaim> ApplicationUserClaim => Set<ApplicationUserClaim>();
        public virtual DbSet<ApplicationUserRole> ApplicationUserRole => Set<ApplicationUserRole>();
        public virtual DbSet<ApplicationUserLogin> ApplicationUserLogin => Set<ApplicationUserLogin>();
        public virtual DbSet<ApplicationRoleClaim> ApplicationRoleClaim => Set<ApplicationRoleClaim>();
        public virtual DbSet<ApplicationUserToken> ApplicationUserToken => Set<ApplicationUserToken>();

        // User information
        public virtual DbSet<UserInfo> UserInfos => Set<UserInfo>();

        
    }
}
