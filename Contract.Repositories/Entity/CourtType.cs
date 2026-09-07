namespace Contract.Repositories.Entity
{
    public class CourtType
    {
        public int CourtTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Court> Courts { get; set; }
            = new List<Court>();
    }
}