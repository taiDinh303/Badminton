namespace Contract.Repositories.AuthEntity
{
    public class VerificationCode
    {
        public int VerificationCodeId { get; set; }

        public int UserId { get; set; }

        public string CodeHash { get; set; } = null!;

        public string Type { get; set; } = null!;

        public DateTime ExpiredAt { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
