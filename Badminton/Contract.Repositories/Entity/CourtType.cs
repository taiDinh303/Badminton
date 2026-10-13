using M.Core.Base;

namespace Contract.Repositories.Entity
{
    /// <summary>Master data: court kinds (Standard / VIP / Premium).
    /// INHERITANCE: extends <see cref="NamedIntMasterEntity"/> — Name/Description/
    /// IsActive + id/audit all come from the base; this class adds nothing,
    /// which is the whole point of a shared master base (Open/Closed).</summary>
    public class CourtType : NamedIntMasterEntity
    {
    }
}
