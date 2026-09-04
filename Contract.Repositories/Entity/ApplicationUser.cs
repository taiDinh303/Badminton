using Microsoft.AspNetCore.Identity;
using Core.Utils;
using Contract.Repositories.BookingEntity;

namespace Contract.Repositories.Entities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? CreatedBy { get; set; }
        public string? LastUpdatedBy { get; set; }
        public string? DeletedBy { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset LastUpdatedTime { get; set; }
        public DateTimeOffset? DeletedTime { get; set; }
        public virtual UserInfo? UserInfo { get; set; }
        public ApplicationUser()
        {
            CreatedTime = CoreHelper.SystemTimeNow;
            LastUpdatedTime = CreatedTime;
        }
    }
}
