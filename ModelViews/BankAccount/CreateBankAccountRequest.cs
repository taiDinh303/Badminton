namespace ModelViews.BankAccount
{
    public class CreateBankAccountRequest
    {
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountHolder { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
    }
}