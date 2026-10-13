namespace Contract.Repositories.Entity
{
    public class GroupMember
    {
        public int GroupId { get; set; }
        public PlayGroup? Group { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
