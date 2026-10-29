using Badminton.Core.Base;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Payment;
using Contract.Services.Voucher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelViews.Booking;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class SecurityFixTests
{
    private const string Secret = "synthetic-webhook-key";
    private static VnPayGateway Vn() => new(new VnPaySettings { MerchantCode = "M00001", SecretKey = Secret }, new NoNetwork());
    private static TestHost HostWithOnlineGateway() => new(configure: services => {
        services.RemoveAll<IPaymentGateway>(); services.AddSingleton<IPaymentGateway>(Vn());
    });

    [Theory]
    [InlineData("reference")]
    [InlineData("amount")]
    [InlineData("merchant")]
    [InlineData("method")]
    [InlineData("status")]
    [InlineData("unsigned")]
    public async Task Online_webhook_cannot_settle_a_different_or_invalid_transaction(string changed)
    {
        await using var h = HostWithOnlineGateway();
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await SeedPayment(h, booking.Id, h.Alice, booking.PayableAmount, changed == "method" ? PaymentMethod.MoMo : PaymentMethod.VnPay);
        var callback = PaymentCallbackFixtures.VnPay(Secret, changed == "reference" ? "ORDER-A" : "ORDER-B",
            changed == "amount" ? 1 : booking.PayableAmount, changed == "merchant" ? "OTHER" : "M00001", changed == "status" ? "02" : "00");
        if (changed == "unsigned") callback = callback with { Body = callback.Body.Replace("vnp_Amount=", "vnp_Amount=1") };
        var result = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("VnPay", new("ORDER-B", "success", callback.Body, booking.PayableAmount)));
        Assert.False(result.Accepted);
        Assert.Equal(BookingStatus.Pending, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync(p => p.BookingId == booking.Id && p.Status == PaymentStatus.Success)));
    }

    [Fact]
    public async Task Valid_online_callback_settles_once_and_ignores_unsigned_wrapper_status_and_amount()
    {
        await using var h = HostWithOnlineGateway();
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await SeedPayment(h, booking.Id, h.Alice, booking.PayableAmount);
        var callback = PaymentCallbackFixtures.VnPay(Secret, "ORDER-B", booking.PayableAmount);
        var payload = new ModelViews.Payment.PaymentWebhookRequest("ORDER-B", "failure", callback.Body, 1);
        var result = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("VnPay", payload));
        Assert.True(result.Accepted);
        var replay = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("VnPay", payload));
        Assert.True(replay.IdempotentDuplicate);
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync(p => p.BookingId == booking.Id)));
    }

    [Theory]
    [InlineData("MoMo")]
    [InlineData("Zalopay")]
    public async Task Changing_signed_callback_data_or_wrapper_is_rejected(string provider)
    {
        IPaymentGateway gateway = provider == "MoMo"
            ? new MoMoGateway(new MoMoSettings { AppId = "test", SecretKey = Secret, AccessKey = "audit-access" }, new NoMomoNetwork())
            : new ZalopayGateway(new ZalopaySettings { AppId = "1234", ApiKey = "key1", ApiSecret = Secret, CallbackKey = Secret }, new NoZaloNetwork());
        var valid = provider == "MoMo" ? PaymentCallbackFixtures.MoMo(Secret, "ORDER-B", result: 99) : PaymentCallbackFixtures.Zalo(Secret, "ORDER-B");
        Assert.True((await gateway.VerifyWebhookAsync(valid)).Verified);
        Assert.False((await gateway.VerifyWebhookAsync(valid with { GatewayRef = "ORDER-A" })).Verified);
        var body = provider == "MoMo" ? valid.Body.Replace("\"resultCode\":99", "\"resultCode\":0") : valid.Body.Replace("150000", "150001");
        Assert.False((await gateway.VerifyWebhookAsync(valid with { Body = body })).Verified);
        var legacy = provider == "MoMo"
            ? "{\"orderId\":\"ORDER-B\",\"responseStatus\":\"0\",\"responseKey\":\"" + MoMoSigner.ResponseKey("ORDER-B", Secret) + "\"}"
            : "{\"callback\":{\"orderId\":\"ORDER-B\",\"callbackStatus\":1}}";
        Assert.False((await gateway.VerifyWebhookAsync(valid with { Body = legacy })).Verified);
    }

    [Fact]
    public async Task Unsupported_online_refund_rolls_back_cancellation_voucher_and_points()
    {
        await using var h = HostWithOnlineGateway();
        await h.AddVoucherAsync("HOLD", 10, 1, 10000);
        await h.CountPointsIntoAsync(h.Alice, 1000, "balance");
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), 1000, "HOLD");
        await h.ConfirmOnSiteAsync(booking.Id, h.Manager);
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var receipt = await db.Payments.SingleAsync(p => p.BookingId == booking.Id);
            receipt.Method = PaymentMethod.VnPay;
            await db.SaveChangesAsync();
        });
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(booking.Id, h.Alice)));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(1, (await h.GetVoucherAsync("HOLD")).UsedQty);
        Assert.Equal(0, await h.CountRefundRowsAsync(booking.Id));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(h.Alice)));
    }

    [Fact]
    public async Task Nonmember_is_denied_while_owner_and_joined_member_can_read_members()
    {
        await using var h = new TestHost();
        var group = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CreateAsync("Private", h.Alice, 2));
        h.ActAs(h.Carol, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().GetMembersAsync(group.Id)));
        h.ActAs(h.Alice, Role.Customer);
        Assert.Single(await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().GetMembersAsync(group.Id)));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Bob, group.InviteCode));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Equal(2, (await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().GetMembersAsync(group.Id))).Count);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("percent")]
    [InlineData("date")]
    [InlineData("range")]
    [InlineData("quantity")]
    [InlineData("type")]
    [InlineData("code")]
    public async Task Invalid_vouchers_are_rejected_before_persistence(string field)
    {
        await using var h = new TestHost();
        var request = new ModelViews.Voucher.CreateVoucherRequest("SAFE", "Fixed", 10000);
        request = field switch {
            "value" => request with { Value = -10000 }, "percent" => request with { Type = "Percent", Value = 101 },
            "date" => request with { ValidUntil = "not-a-date" }, "range" => request with { ValidFrom = "2026-12-20", ValidUntil = "2026-12-19" },
            "quantity" => request with { TotalQty = 0 }, "type" => request with { Type = "invented" }, _ => request with { Code = new string('x', 31) }
        };
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IVoucherService>().CreateAsync(request, h.Manager)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Vouchers.CountAsync()));
    }

    [Fact]
    public async Task Voucher_dates_are_correct_and_fixed_discount_never_exceeds_slot_price()
    {
        await using var h = new TestHost();
        var voucher = await h.AddVoucherAsync("LARGE", 10, 1, 1000000);
        var view = await h.RunScopeAsync(sp => sp.GetRequiredService<IVoucherService>().GetByCodeAsync("LARGE"));
        Assert.Equal(voucher.ValidFrom.ToString("yyyy-MM-dd"), view.ValidFrom);
        Assert.Equal(voucher.ValidUntil!.Value.ToString("yyyy-MM-dd"), view.ValidUntil);
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, VoucherCode: "LARGE");
        var quote = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(request, h.Alice));
        var booked = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(request, h.Alice));
        Assert.Equal(quote.Total, quote.VoucherDiscount);
        Assert.Equal(0, booked.PayableAmount);
        Assert.Equal(quote.VoucherDiscount, booked.DiscountAmount);
    }

    [Fact]
    public async Task Unknown_voucher_is_rejected_by_both_quote_and_submission()
    {
        await using var h = new TestHost();
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, VoucherCode: "UNKNOWN");
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(request, h.Alice)));
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(request, h.Alice)));
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("conflict")]
    public async Task Waitlist_keeps_candidate_when_booking_fails_temporarily(string failure)
    {
        await using var h = new TestHost();
        var id = await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var entry = new WaitlistEntry { UserId = h.Bob, CourtId = h.Court1, SlotId = h.SlotNoon,
                Date = h.FutureDate, PlayerCount = 1, Status = WaitlistStatus.Waiting };
            db.Set<WaitlistEntry>().Add(entry);
            await db.SaveChangesAsync();
            return entry.Id;
        });
        await h.RunScopeAsync(async sp => {
            var booking = System.Reflection.DispatchProxy.Create<IBookingService, FailingBooking>();
            ((FailingBooking)(object)booking).Failure = failure == "timeout" ? new TimeoutException("Synthetic database timeout") : new ConflictException("Synthetic slot race");
            var service = new WaitlistService(sp.GetRequiredService<Contract.Repositories.UnitOfWork.IUnitOfWork>(),
                sp.GetRequiredService<Badminton.Core.Security.ICurrentUser>(), sp.GetRequiredService<Contract.Services.Notify.INotifyService>(), booking);
            if (failure == "timeout") await Assert.ThrowsAsync<TimeoutException>(() => service.PromoteAsync(h.Court1, h.SlotNoon, h.FutureDate));
            else Assert.Null(await service.PromoteAsync(h.Court1, h.SlotNoon, h.FutureDate));
        });
        var row = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().WaitlistEntries.SingleAsync(w => w.Id == id));
        Assert.Equal(WaitlistStatus.Waiting, row.Status);
        Assert.Null(row.ResolvedAt);
        Assert.Null(row.DeclineReason);
    }

    public class FailingBooking : System.Reflection.DispatchProxy
    {
        public Exception Failure = null!;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => Task.FromException<BookingView>(Failure);
    }

    [Theory]
    [InlineData("{\"type\":\"1\",\"data\":\"{}\",\"mac\":\"x\"}")]
    [InlineData("{\"type\":null,\"data\":\"{}\",\"mac\":\"x\"}")]
    public async Task Malformed_Zalo_type_is_rejected_without_exception(string body)
    {
        var gateway = new ZalopayGateway(new ZalopaySettings { AppId = "1234", ApiKey = "key1", ApiSecret = Secret, CallbackKey = Secret }, new NoZaloNetwork());
        Assert.False((await gateway.VerifyWebhookAsync(new("Zalopay", "ORDER", body, null, new Dictionary<string, string>()))).Verified);
    }

    [Fact]
    public void Vnpay_signature_uses_documented_url_encoded_canonical_payload()
    {
        const string canonical = "vnp_Amount=15000000&vnp_OrderInfo=Dat+san+A%26B&vnp_TxnRef=ORDER";
        var expected = Convert.ToHexString(System.Security.Cryptography.HMACSHA512.HashData(
            System.Text.Encoding.UTF8.GetBytes(Secret), System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        Assert.Equal(expected, VnPaySigner.Sign(Secret, new Dictionary<string, string> {
            ["vnp_TxnRef"] = "ORDER", ["vnp_OrderInfo"] = "Dat san A&B", ["vnp_Amount"] = "15000000", ["vnp_SecureHashType"] = "HMACSHA512"
        }));
    }

    [Theory]
    [InlineData("MoMo")]
    [InlineData("VnPay")]
    [InlineData("Zalopay")]
    public async Task Online_refund_never_reports_success_without_a_transport(string provider)
    {
        IPaymentGateway gateway = provider switch {
            "MoMo" => new MoMoGateway(new MoMoSettings { SecretKey = Secret, AccessKey = "audit-access" }, new NoMomoNetwork()),
            "VnPay" => Vn(),
            _ => new ZalopayGateway(new ZalopaySettings { ApiKey = "key1", ApiSecret = Secret, CallbackKey = Secret }, new NoZaloNetwork())
        };
        var result = await gateway.RefundAsync("ORDER", 100000, "Regression");
        Assert.False(result.Ok);
        Assert.Equal(0, result.Amount);
        Assert.Contains("ONLINE_REFUND_UNAVAILABLE", result.Error);
    }

    [Fact]
    public async Task Legacy_mixed_case_voucher_remains_usable_on_both_database_providers()
    {
        await using var h = new TestHost();
        await h.AddVoucherAsync("LegacyCode", 10, 1, 10000);
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, VoucherCode: " legacycode ");
        var quote = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(request, h.Alice));
        var booked = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(request, h.Alice));
        Assert.Equal(10000, quote.VoucherDiscount);
        Assert.Equal(quote.VoucherDiscount, booked.DiscountAmount);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Vouchers.Where(v => v.Code == "LegacyCode").Select(v => v.UsedQty).SingleAsync()));
    }

    private static Task SeedPayment(TestHost h, int booking, int user, int amount, PaymentMethod method = PaymentMethod.VnPay) => h.RunScopeAsync(async sp => {
        var db = sp.GetRequiredService<BadmintonBookingDbContext>();
        db.Payments.Add(new Payment { BookingId = booking, UserId = user, Amount = amount, Method = method, Status = PaymentStatus.Pending, GatewayRef = "ORDER-B" });
        await db.SaveChangesAsync();
    });
    private sealed class NoNetwork : IVnPayClient {
        public Task<VnPayCreateResult> CreatePayUrlAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct) => throw new InvalidOperationException("No network in regression tests.");
    }
    private sealed class NoMomoNetwork : IMomoApiClient {
        public Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest request, CancellationToken ct) => throw new InvalidOperationException("No network in regression tests.");
    }
    private sealed class NoZaloNetwork : IZalopayClient {
        public Task<ZpOrderCreate> CreateOrderAsync(ZpCreateOrderRequest request, CancellationToken ct) => throw new InvalidOperationException("No network in regression tests.");
    }
}
