using M.Core.Base;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// A customer's stored bank account (for BankTransfer payments).
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class BankAccount : IntBaseEntity
    {
        public int? UserId { get; set; }
        public User? User { get; set; }

        public string BankName { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public string AccountHolderName { get; set; } = null!;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
