using Badminton.API.Configuration;
using Badminton.Core.Base;
using Badminton.Core.Store;
using Contract.Repositories.Entity;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class BankTransferTests
{
    private static BankTransferSettings Settings() => new()
    {
        Branches = new() { ["7"] = new() { BankBin = "970422", AccountNumber = "000123456789", AccountName = "SAN CAU LONG & CO" } }
    };

    [Fact]
    public void Instructions_use_booking_amount_branch_reference_and_utc_expiry()
    {
        var expiry = new DateTime(2026, 11, 14, 10, 0, 0);
        var result = BankTransferInstructions.Create(Settings(), new Booking { Id = 123, BranchId = 7, PayableAmount = 120000, PendingExpiresAt = expiry })!;
        Assert.Equal("000123456789", result.AccountNumber);
        Assert.Equal(120000, result.Amount);
        Assert.Equal("BM123", result.TransferContent);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAtUtc.Kind);
        Assert.True(result.RequiresStaffConfirmation);
        Assert.Contains("amount=120000&addInfo=BM123&accountName=SAN%20CAU%20LONG%20%26%20CO", result.QrImageUrl);
        Assert.Equal("img.vietqr.io", new Uri(result.QrImageUrl).Host);
    }

    [Fact]
    public void Unconfigured_branch_never_falls_back_to_another_account()
        => Assert.Null(BankTransferInstructions.Create(Settings(), new Booking { BranchId = 8 }));

    [Theory]
    [InlineData("../evil", "123456")]
    [InlineData("970422", "123&amount=1")]
    public void Malformed_configuration_fails_closed(string bank, string account)
    {
        var settings = Settings(); settings.Branches["7"].BankBin = bank; settings.Branches["7"].AccountNumber = account;
        Assert.Throws<ConflictException>(() => BankTransferInstructions.Create(settings, new Booking { BranchId = 7 }));
    }

    [Fact]
    public void Zero_amount_does_not_offer_a_bank_transfer()
        => Assert.Throws<ConflictException>(() => BankTransferInstructions.Create(Settings(), new Booking { BranchId = 7, PayableAmount = 0, PendingExpiresAt = DateTime.UtcNow }));
}
