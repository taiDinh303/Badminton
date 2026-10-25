using Badminton.Core.Base;

namespace Contract.Repositories.Entity
{
    public class PlayGroup : IntBaseEntity
    {
        public string Name { get; set; } = null!;
        public int OwnerId { get; set; }
        public User? Owner { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? InviteTokenHash { get; set; }
        public int MaxMembers { get; set; } = 10;
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
}
