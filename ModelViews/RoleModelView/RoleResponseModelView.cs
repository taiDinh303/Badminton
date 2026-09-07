namespace ModelViews.RoleModelView
{
    public class RoleResponseModelView
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;    // Role name (e.g., Admin, User)
        public string NormalizedName { get; set; } = string.Empty; // Normalized role name (e.g., ADMIN, USER)
        public string Description { get; set; } = string.Empty;

        // Audit information
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTimeOffset LastUpdatedTime { get; set; }
    }
}
