namespace M.Core.Base
{
    /// <summary>
    /// INHERITANCE (kế thừa): the base of every domain entity. One place
    /// defines the int PK + audit/soft-delete fields; all entities inherit
    /// them so the convention is identical across the whole model. (EF creates
    /// the database from this model, so these audit columns become real DB
    /// columns automatically.)
    /// </summary>
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

    /// <summary>
    /// Second level of INHERITANCE: master/reference data (CourtType, and any
    /// future master table) adds Name/Description/IsActive on top of
    /// <see cref="IntBaseEntity"/>. Adding a new master type = a new class;
    /// the fields come from here (Open/Closed).
    /// </summary>
    public abstract class NamedIntMasterEntity : IntBaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
