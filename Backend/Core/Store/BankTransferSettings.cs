using System.Text.RegularExpressions;

namespace Badminton.Core.Store;

public sealed class BankTransferSettings
{
    public Dictionary<string, BankTransferAccount> Branches { get; set; } = new();
}

public sealed class BankTransferAccount
{
    public string BankBin { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";

    public bool IsValid() => Regex.IsMatch(BankBin, @"\A[0-9]{6}\z")
        && Regex.IsMatch(AccountNumber, @"\A[0-9]{6,19}\z")
        && !string.IsNullOrWhiteSpace(AccountName) && AccountName.Length <= 100
        && !AccountName.Any(char.IsControl);
}
