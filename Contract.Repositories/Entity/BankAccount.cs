namespace Contract.Repositories.AuthEntity
{
    public class BankAccount
    {
        public int BankAccountId { get; set; }

        public int UserId { get; set; }

        public string BankName { get; set; } = string.Empty;

        public string AccountNumber { get; set; } = string.Empty;

        public string AccountHolder { get; set; } = string.Empty;

        public string AccountType { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}