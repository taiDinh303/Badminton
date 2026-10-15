namespace M.Core.Base
{
    public abstract class IntBaseEntity
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Soft-delete marker. Non-null means the row is logically gone.</summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>Derived soft-delete flag so repositories can filter uniformly.</summary>
        public bool IsDeleted => DeletedAt.HasValue;
    }

    public abstract class NamedIntMasterEntity : IntBaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
