namespace Contract.Repositories.Entity
{
    public class AuditLog
    {
        public long Id { get; set; }
        public int? ActorId { get; set; }
        public User? Actor { get; set; }
        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public string? EntityId { get; set; }
        public string? BeforeJson { get; set; }
        public string? AfterJson { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
