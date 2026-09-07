namespace Contract.Repositories.Entity
{
    public class Branch
    {
        public int BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<Court> Courts { get; set; } = new List<Court>();
    }
}