using Core.Utils;
using Microsoft.AspNetCore.Identity;

namespace Contract.Repositories.AuthEntity
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string? CreatedBy { get; set; }
        public string? LastUpdatedBy { get; set; }
        public string? DeletedBy { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset LastUpdatedTime { get; set; }
        public DateTimeOffset? DeletedTime { get; set; }
        public string? Description { get; set; }
        public ApplicationRole()
        {
            CreatedTime = CoreHelper.SystemTimeNow;
            LastUpdatedTime = CreatedTime;
        }
    }
}