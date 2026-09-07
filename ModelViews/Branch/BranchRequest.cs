namespace ModelViews.Branch
{
    public class BranchRequest
    {
        public string BranchName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}