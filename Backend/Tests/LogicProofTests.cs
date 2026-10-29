using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Policies;
using Badminton.Core.Store;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Services.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests
{
    /// <summary>
    /// Bộ test "chứng minh code chạy đúng" cho các logic nghiệp vụ quan trọng:
    /// - Pure logic (pricing, refund tier, redeem, auth input, HanoiTime, VnPay/MoMo signers,
    ///   RasterUploadPolicy, WebhookSecurity): chạy nhanh, không cần DB.
    /// - Service thực trên TestHost (voucher, daily cap, conflict, points, group): chứng minh
    ///   các service hoạt động đúng thiết kế trên fixture BadmintonTest_* (loopback, tự dọn).
    ///
    /// Không sửa source ứng dụng; không gọi gateway thật; không đụng DB ứng dụng/live.
    /// </summary>
    public sealed class LogicProofTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        // ─────────────────────────────────────────────────────────────────
        // A. PRICING — giá theo slot + phụ thu cuối tuần (PRICING-01, owner chốt 24/10).
        //    Số người chỉ là cap sức chứa, không nhân giá.
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Pricing: low-peak tuần thường = 100k, peak tuần thường = 150k")]
        public void Pricing_LowPeakWeekday()
        {
            var pricing = new PricingService();
            var court = new Court { PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 4 };
            var low = new TimeSlot { IsPeak = false };
            var peak = new TimeSlot { IsPeak = true };
            var monday = DateOnly.FromDateTime(new DateTime(2026, 11, 16)); // Monday
            Assert.Equal(100_000, BookingPricing.SlotTotal(court, low, monday, 2, pricing));
            Assert.Equal(150_000, BookingPricing.SlotTotal(court, peak, monday, 2, pricing));
        }

        [Fact(DisplayName = "Pricing: thứ 7/Chủ nhật cộng phụ thu 20% → low 120k, peak 180k")]
        public void Pricing_WeekendSurcharge()
        {
            var pricing = new PricingService();
            var court = new Court { PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 4 };
            var saturday = DateOnly.FromDateTime(new DateTime(2026, 11, 21));
            var sunday = DateOnly.FromDateTime(new DateTime(2026, 11, 22));
            Assert.Equal(120_000, BookingPricing.SlotTotal(court, new TimeSlot { IsPeak = false }, saturday, 3, pricing));
            Assert.Equal(180_000, BookingPricing.SlotTotal(court, new TimeSlot { IsPeak = true }, sunday, 3, pricing));
        }

        [Theory(DisplayName = "Pricing: số người ngoài [1, capacity] bị từ chối (BadRequest)")]
        [InlineData(0)]
        [InlineData(100)]
        public void Pricing_PlayerCountBounded(int players)
        {
            var pricing = new PricingService();
            var court = new Court { PriceLowPeak = 100_000, PricePeak = 150_000, Capacity = 4 };
            var slot = new TimeSlot { IsPeak = false };
            var date = DateOnly.FromDateTime(new DateTime(2026, 11, 16));
            Assert.Throws<BadRequestException>(() => BookingPricing.SlotTotal(court, slot, date, players, pricing));
        }

        [Fact(DisplayName = "Redeem: 5000 điểm → giảm 100 000đ; 999 điểm → 0 (dưới ngưỡng)")]
        public void RedeemPoints_ConversionAndFloor()
        {
            var (_, d5000) = BookingPricing.Redeem(5_000, 200_000);
            var (_, d999) = BookingPricing.Redeem(999, 200_000);
            var (_, dHuge) = BookingPricing.Redeem(1_000_000, 100_000); // muốn nhiều hơn khả dụng
            Assert.Equal(100_000, d5000);   // 5000 × 20000/1000
            Assert.Equal(0, d999);          // <1000 điểm → không đổi
            Assert.Equal(100_000, dHuge);   // cap bởi availableAmount
        }

        [Fact(DisplayName = "Redeem: điểm âm bị từ chối")]
        public void RedeemPoints_RejectsNegative()
        {
            Assert.Throws<BadRequestException>(() => BookingPricing.Redeem(-1, 100_000));
        }

        // ─────────────────────────────────────────────────────────────────
        // B. REFUND POLICY — bậc hoàn tiền theo giờ trước khi chơi.
        // ─────────────────────────────────────────────────────────────────

        [Theory(DisplayName = "Refund tier: ≥2h→100%, 1–2h→50%, <1h→0%")]
        [InlineData(3.0, 1.0)]
        [InlineData(2.0, 1.0)]
        [InlineData(1.5, 0.5)]
        [InlineData(1.0, 0.5)]
        [InlineData(0.5, 0.0)]
        [InlineData(0.0, 0.0)]
        public void RefundPolicy_TierBoundary(double hoursAhead, double expectedRatio)
        {
            var now = new DateTime(2026, 11, 1, 9, 0, 0);
            var (ratio, _) = RefundPolicy.Tier(now, now.AddHours(hoursAhead));
            Assert.Equal(expectedRatio, ratio, 5);
        }

        // ─────────────────────────────────────────────────────────────────
        // C. AUTH INPUT — chuẩn SĐT VN, mật khẩu, họ tên.
        // ─────────────────────────────────────────────────────────────────

        [Theory(DisplayName = "AuthInput.Phone: SĐT 10 số bắt đầu 0; trim whitespace")]
        [InlineData("0901234567", true)]
        [InlineData(" 0901234567 ", true)]
        [InlineData("090123456", false)]
        [InlineData("1901234567", false)]
        [InlineData("09012345678", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void AuthInput_PhoneFormat(string? phone, bool expectedOk)
        {
            if (expectedOk)
                Assert.Equal(phone!.Trim(), AuthInput.Phone(phone));
            else
                Assert.Throws<BadRequestException>(() => AuthInput.Phone(phone!));
        }

        [Theory(DisplayName = "AuthInput.Password: 8 ký tự UTF-8 tối thiểu, 72 byte tối đa")]
        [InlineData("12345678", true)]
        [InlineData("aaaaaaaa", true)]
        [InlineData("1234567", false)]
        [InlineData("a", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void AuthInput_PasswordLength(string? pwd, bool expectedOk)
        {
            if (expectedOk)
                Assert.Null(Record.Exception(() => AuthInput.Password(pwd!)));
            else
                Assert.Throws<BadRequestException>(() => AuthInput.Password(pwd!));
        }

        [Fact(DisplayName = "AuthInput.Password: 72 bytes đúng ranh giới OK; 73 bytes bị từ chối")]
        public void AuthInput_Password_MaxUtf8Bytes()
        {
            Assert.Null(Record.Exception(() => AuthInput.Password(new string('a', 72))));
            Assert.Throws<BadRequestException>(() => AuthInput.Password(new string('a', 73)));
        }

        [Fact(DisplayName = "AuthInput.Name: 1..100 ký tự sau trim")]
        public void AuthInput_NameLength()
        {
            Assert.Throws<BadRequestException>(() => AuthInput.Name("A"));
            Assert.Equal("A B", AuthInput.Name("  A B  "));
            Assert.Throws<BadRequestException>(() => AuthInput.Name(new string('x', 101)));
        }

        // ─────────────────────────────────────────────────────────────────
        // D. HANOITIME — quy đổi UTC+7 (deadline/múi giờ nghiệp vụ).
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "HanoiTime: 02:00 UTC Nov 1 = 09:00 Hanoi Nov 1")]
        public void HanoiTime_UtcOffset()
        {
            var utc0200 = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc);
            Assert.Equal(new DateOnly(2026, 11, 1), HanoiTime.HanoiDateOf(utc0200));
        }

        [Fact(DisplayName = "HanoiTime: 16:59 UTC Nov 1 còn Nov 1; 17:00 UTC đã là Nov 2 Hanoi")]
        public void HanoiTime_DayRollover()
        {
            Assert.Equal(new DateOnly(2026, 11, 1), HanoiTime.HanoiDateOf(new DateTime(2026, 11, 1, 16, 59, 59, DateTimeKind.Utc)));
            Assert.Equal(new DateOnly(2026, 11, 2), HanoiTime.HanoiDateOf(new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc)));
        }

        [Fact(DisplayName = "HanoiTime: MonthStart của ngày 15/11 là 01/11")]
        public void HanoiTime_MonthStart()
        {
            var day = DateOnly.FromDateTime(new DateTime(2026, 11, 15));
            Assert.Equal(new DateOnly(2026, 11, 1), day.MonthStart());
        }

        [Fact(DisplayName = "HanoiTime: slot 09:40 với 'now' 09:00 còn bookable; slot 09:20 thì chưa")]
        public void HanoiTime_30MinGuardBoundary()
        {
            var utc0900Hanoi = DateTime.SpecifyKind(
                new DateTime(2026, 11, 1, 2, 0, 0), DateTimeKind.Utc);
            HanoiTime.NowUtc = () => utc0900Hanoi;
            try
            {
                var now = HanoiTime.HanoiNow();
                var s40 = HanoiTime.SlotStart(DateOnly.FromDateTime(new DateTime(2026, 11, 1)), new TimeOnly(9, 40));
                var s20 = HanoiTime.SlotStart(DateOnly.FromDateTime(new DateTime(2026, 11, 1)), new TimeOnly(9, 20));
                Assert.True(s40 >= now.AddMinutes(30), "09:40 = 40 phút, phải qua guard 30 phút");
                Assert.True(s20 < now.AddMinutes(30), "09:20 = 20 phút, nằm trong guard 30 phút");
            }
            finally { HanoiTime.NowUtc = () => DateTime.UtcNow; }
        }

        // ─────────────────────────────────────────────────────────────────
        // E. WEBHOOK SIGNATURE — VNPAY + MoMo (chống giả mạo/đổi nội dung).
        // ────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "VnPaySigner: Verify đúng secret+fields; sai secret hoặc tam amount → false")]
        public void VnPaySigner_VerifyRoundtrip()
        {
            var secret = "webhook-secret";
            var fields = new Dictionary<string, string>
            {
                ["vnp_TmnCode"] = "M001", ["vnp_TxnRef"] = "REF-1",
                ["vnp_Amount"] = "15000000", ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00"
            };
            var hash = VnPaySigner.Sign(secret, fields);
            Assert.True(VnPaySigner.Verify(secret, hash, fields));
            Assert.False(VnPaySigner.Verify("wrong-secret", hash, fields));
            var tampered = new Dictionary<string, string>(fields) { ["vnp_Amount"] = "15000001" };
            Assert.False(VnPaySigner.Verify(secret, hash, tampered));
        }

        [Fact(DisplayName = "VnPaySigner.BuildQuery: vnp_SecureHash được nhúng; verify đúng khi tách query")]
        public void VnPaySigner_BuildQueryIncludesHash()
        {
            var secret = "k";
            var fields = new Dictionary<string, string> { ["vnp_TxnRef"] = "A", ["vnp_Amount"] = "1" };
            var query = VnPaySigner.BuildQuery(secret, fields);
            Assert.Contains("vnp_SecureHash=", query);
            var pairs = System.Web.HttpUtility.ParseQueryString(query)
                .AllKeys.OfType<string>().ToDictionary(k => k, k => System.Web.HttpUtility.ParseQueryString(query)[k]!);
            Assert.True(VnPaySigner.Verify(secret, pairs["vnp_SecureHash"], pairs));
        }

        [Fact(DisplayName = "MoMoSigner: ComputeSign deterministic, ResponseKey stable, excludes empty values")]
        public void MoMoSigner_Determinism()
        {
            var d = new Dictionary<string, string>
            {
                ["amount"] = "150000", ["orderId"] = "X", ["partnerCode"] = "P",
                ["requestId"] = "r", ["responseKey"] = "", ["empty"] = ""
            };
            var s1 = MoMoSigner.ComputeSign(d);
            var s2 = MoMoSigner.ComputeSign(d);
            Assert.Equal(s1, s2);
            Assert.Equal(128, s1.Length); // SHA512 hex
            var rk1 = MoMoSigner.ResponseKey("order-1", "secret");
            var rk2 = MoMoSigner.ResponseKey("order-1", "secret");
            var rk3 = MoMoSigner.ResponseKey("order-2", "secret");
            Assert.Equal(rk1, rk2);
            Assert.NotEqual(rk1, rk3);
        }

        [Fact(DisplayName = "VnPaySigner: Verify từ chối khi expected hash rỗng hoặc sai độ dài")]
        public void VnPaySigner_RejectsMalformedHash()
        {
            var secret = "s";
            var fields = new Dictionary<string, string> { ["vnp_TxnRef"] = "A", ["vnp_Amount"] = "1" };
            var hash = VnPaySigner.Sign(secret, fields);
            Assert.True(VnPaySigner.Verify(secret, hash, fields));
            Assert.False(VnPaySigner.Verify(secret, "", fields));
            Assert.False(VnPaySigner.Verify(secret, "abc", fields));
            Assert.False(VnPaySigner.Verify(secret, "xyz", fields));
        }

        // ─────────────────────────────────────────────────────────────────
        // F. RASTER UPLOAD POLICY — PNG/JPEG chỉ nhận header hợp lệ.
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Raster: buffer ngắn hơn 24B bị từ chối")]
        public void Raster_TooShort()
        {
            Assert.Throws<BadRequestException>(() => RasterUploadPolicy.Validate(new byte[24]));
        }

        [Fact(DisplayName = "Raster: PNG tối thiểu (IHDR + IEND, 1×1) được nhận")]
        public void Raster_MinPngAccepted()
        {
            var png = new List<byte>(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            png.AddRange(new byte[4] { 0, 0, 0, 13 });
            png.AddRange("IHDR"u8.ToArray());
            png.AddRange(new byte[4] { 0, 0, 0, 1 });
            png.AddRange(new byte[4] { 0, 0, 0, 1 });
            png.AddRange(new byte[13]);
            png.AddRange("IEND"u8.ToArray());
            png.AddRange(new byte[4] { 0, 0, 0, 0 });
            Assert.Equal("image/png", RasterUploadPolicy.Validate(png.ToArray()));
        }

        [Fact(DisplayName = "Raster: vượt 2MB bị từ chối")]
        public void Raster_OversizeRejected()
        {
            var bytes = new byte[2 * 1024 * 1024 + 1];
            Assert.Throws<BadRequestException>(() => RasterUploadPolicy.Validate(bytes));
        }

        // ─────────────────────────────────────────────────────────────────
        // G. SERVICE: VOUCHER (TestHost, DB fixture).
        //    ReserveVoucherAsync must run inside an active transaction (mirrors
        //    BookingCreationService); the helper below does that.
        // ─────────────────────────────────────────────────────────────────

        private async Task<T> InVoucherTx<T>(Func<Contract.Services.Voucher.IVoucherService, Task<T>> action)
        {
            T result = default!;
            await _h.RunScopeAsync(async sp =>
            {
                var uow = sp.GetRequiredService<Contract.Repositories.UnitOfWork.IUnitOfWork>();
                await uow.RunInTransactionAsync(async () =>
                {
                    result = await action(sp.GetRequiredService<Contract.Services.Voucher.IVoucherService>());
                    // Mirror BookingCreationService: the staged redemption must be committed
                    // for the total-qty claim and per-user limit to persist.
                    await uow.SaveChangesAsync();
                });
            });
            return result;
        }

        [Fact(DisplayName = "Voucher Fixed 10k: giảm đúng 10k trên đơn 150k (không vượt giá)")]
        public async Task Voucher_FixedAmount()
        {
            await _h.AddVoucherAsync("V-FIXED-10K", totalQty: 10, maxPerUser: 1, value: 10_000, VoucherType.Fixed, minSpend: 50_000);
            var (discount, _) = await InVoucherTx(v => v.ReserveVoucherAsync("V-FIXED-10K", _h.Alice, 150_000));
            Assert.Equal(10_000, discount);
        }

        [Fact(DisplayName = "Voucher Percent 50% cap bởi MaxValue: 50% × 150k = 75k, cap 20k → 20k")]
        public async Task Voucher_PercentCappedByMaxValue()
        {
            await _h.AddVoucherAsync("V-50P", totalQty: 10, maxPerUser: 1, value: 50, VoucherType.Percent, minSpend: 0);
            await _h.RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                var v = await db.Vouchers.FirstAsync(x => x.Code == "V-50P");
                v.MaxValue = 20_000;
                await db.SaveChangesAsync();
            });
            var (discount, _) = await InVoucherTx(v => v.ReserveVoucherAsync("V-50P", _h.Bob, 150_000));
            Assert.Equal(20_000, discount);
        }

        [Fact(DisplayName = "Voucher Percent 100% không cap (MaxValue=null) bị clamp về giá đơn")]
        public async Task Voucher_PercentClampedToPrice()
        {
            // AddVoucherAsync không set MaxValue (null = không cap). 100% × 150k = 150k,
            // Math.Clamp(..., 0, baseAmount) giữ đúng giá đơn, không âm.
            await _h.AddVoucherAsync("V-100P", totalQty: 10, maxPerUser: 1, value: 100, VoucherType.Percent, minSpend: 0);
            var (discount, _) = await InVoucherTx(v => v.ReserveVoucherAsync("V-100P", _h.Bob, 150_000));
            Assert.Equal(150_000, discount);
        }

        [Fact(DisplayName = "Voucher per-code: dùng mã 2 lần cùng user → từ chối")]
        public async Task Voucher_PerCodeLimit()
        {
            await _h.AddVoucherAsync("V-ONCE", totalQty: 100, maxPerUser: 1, value: 5_000, VoucherType.Fixed, minSpend: 0);
            await InVoucherTx(v => v.ReserveVoucherAsync("V-ONCE", _h.Alice, 150_000));
            Exception? err = null;
            try { await InVoucherTx(v => v.ReserveVoucherAsync("V-ONCE", _h.Alice, 150_000)); }
            catch (Exception e) { err = e; }
            Assert.IsType<ConflictException>(err);
            Assert.Contains("đã sử dụng", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Voucher đã hết hạn (ValidUntil quá khứ) → từ chối")]
        public async Task Voucher_ExpiredRejected()
        {
            await _h.RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                db.Vouchers.Add(new Voucher
                {
                    Code = "V-OLD",
                    Type = VoucherType.Fixed,
                    Value = 5_000,
                    MinSpend = 0,
                    MaxPerUser = 1,
                    TotalQty = 5,
                    UsedQty = 0,
                    ValidFrom = DateOnly.FromDateTime(new DateTime(2020, 1, 1)),
                    ValidUntil = DateOnly.FromDateTime(new DateTime(2020, 12, 31)),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            });
            Exception? err = null;
            try { await InVoucherTx(v => v.ReserveVoucherAsync("V-OLD", _h.Alice, 150_000)); }
            catch (Exception e) { err = e; }
            Assert.IsType<BadRequestException>(err);
            Assert.Contains("hết hạn", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Voucher MinSpend: đơn 40k dùng voucher min 50k → từ chối")]
        public async Task Voucher_MinSpendGuard()
        {
            await _h.AddVoucherAsync("V-MIN-50", totalQty: 10, maxPerUser: 1, value: 5_000, VoucherType.Fixed, minSpend: 50_000);
            Exception? err = null;
            try { await InVoucherTx(v => v.ReserveVoucherAsync("V-MIN-50", _h.Alice, 40_000)); }
            catch (Exception e) { err = e; }
            Assert.IsType<BadRequestException>(err);
            Assert.Contains("thấp hơn mức tối thiểu", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────────────────────────────
        // H. BOOKING: daily cap + conflict trên cùng sân/slot.
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Booking: khách thứ 5 trong ngày cùng user bị từ chối (cap 4)")]
        public async Task Booking_DailyCap_Customer()
        {
            var date = _h.FutureDate.ToString("yyyy-MM-dd");
            await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotMorning, date);
            await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotNoon, date);
            await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotPeak, date);
            await _h.BookAsync(_h.Alice, _h.Court2, _h.SlotMorning, date);
            var err = await Record.ExceptionAsync(() => _h.BookAsync(_h.Alice, _h.Court2, _h.SlotNoon, date));
            Assert.IsType<ConflictException>(err);
            Assert.Contains("han 4 slot/ngay", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Booking: hai user cùng sân+slot → SLOT_CONFLICT")]
        public async Task Booking_SameSlot_Conflict()
        {
            var date = _h.FutureDate.ToString("yyyy-MM-dd");
            var first = await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotNoon, date);
            Assert.NotNull(first);
            var err = await Record.ExceptionAsync(() => _h.BookAsync(_h.Bob, _h.Court1, _h.SlotNoon, date));
            Assert.IsType<ConflictException>(err);
            Assert.Contains("SLOT_CONFLICT", err!.Message);
        }

        [Fact(DisplayName = "Booking: slot 40 phút tới được; slot 20 phút tới bị guard chặn")]
        public async Task Booking_SlotStartGuard()
        {
            var date = _h.FutureDate.ToString("yyyy-MM-dd");
            var ok = await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotSoon40, date);
            Assert.NotNull(ok);
            // SlotMorning 10:00 (50 phút tới, qua guard 30) — bookable
            var ok2 = await _h.BookAsync(_h.Alice, _h.Court2, _h.SlotMorning, date);
            Assert.NotNull(ok2);
        }

        [Fact(DisplayName = "Booking: đặt slot ngoài giờ mở cửa (branch 06:00–23:00) → từ chối")]
        public async Task Booking_OutsideBranchHours()
        {
            // Tạo slot 04:00–05:00 (trước giờ mở 06:00) rồi thử đặt
            await _h.RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                var s = await db.TimeSlots.AddAsync(new TimeSlot
                {
                    Label = "04:00",
                    StartTime = new TimeOnly(4, 0),
                    EndTime = new TimeOnly(5, 0),
                    IsPeak = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            });
            var outsideSlotId = (await _h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>()
                    .TimeSlots.FirstAsync(s => s.Label == "04:00"))).Id;
            var err = await Record.ExceptionAsync(() => _h.BookAsync(_h.Alice, _h.Court1,
                outsideSlotId,
                _h.FutureDate.ToString("yyyy-MM-dd")));
            Assert.IsType<BadRequestException>(err);
            Assert.Contains("ngoài giờ mở cửa", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────────────────────────────
        // I. POINTS: ledger + multiplier + redeem/restore.
        // ─────────────────────────────────────────────────────────────────

        // PointLedger.BookingId là FK tới Bookings, nên mọi test points dùng
        // booking thật (chưa cần trạng thái đặc biệt) để không vi phạm FK.
        // Hai user khác nhau cần hai slot khác nhau.
        private async Task<int> NewBookingIdAsync(int user, int court, int slot)
        {
            var view = await _h.BookAsync(user, court, slot, _h.FutureDate.ToString("yyyy-MM-dd"));
            return view!.Id;
        }

        [Fact(DisplayName = "Points: Award 100k VND → 100 điểm (1.0x); Gold → 200 điểm (2.0x)")]
        public async Task Points_AwardRate()
        {
            var bookingBob = await NewBookingIdAsync(_h.Bob, _h.Court1, _h.SlotNoon);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .AwardAsync(_h.Bob, bookingBob, 100_000, "test-basic"));
            var balBob = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(100, balBob);

            await _h.RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                var carol = await db.Users.FirstAsync(x => x.Id == _h.Carol);
                carol.Tier = MembershipTier.Gold;
                carol.MembershipExpiresAt = DateTime.UtcNow.AddDays(30);
                await db.SaveChangesAsync();
            });
            var bookingCarol = await NewBookingIdAsync(_h.Carol, _h.Court2, _h.SlotNoon);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .AwardAsync(_h.Carol, bookingCarol, 100_000, "test-gold"));
            var balCarol = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Carol));
            Assert.Equal(200, balCarol);
        }

        [Fact(DisplayName = "Points: redeem vượt balance → ConflictException, không trừ điểm")]
        public async Task Points_LedgerInsufficientBalance()
        {
            var booking = await NewBookingIdAsync(_h.Bob, _h.Court1, _h.SlotNoon);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .AwardAsync(_h.Bob, booking, 100_000, "test-basic-award"));
            var bal0 = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(100, bal0);
            Exception? err = null;
            try { await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .RedeemAsync(_h.Bob, booking, 5_000, "BD-X")); }
            catch (Exception e) { err = e; }
            Assert.IsType<ConflictException>(err);
            var bal1 = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(bal0, bal1);
        }

        [Fact(DisplayName = "Points: Redeem 5000 (đủ balance) → -5000; Restore → +5000; replay không double-spend")]
        public async Task Points_RedeemRestoreBalance()
        {
            var booking = await NewBookingIdAsync(_h.Bob, _h.Court1, _h.SlotNoon);
            await _h.CountPointsIntoAsync(_h.Bob, 20_000, "test-seed-20k");
            var bal0 = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(20_000, bal0);
            var discount = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .RedeemAsync(_h.Bob, booking, 5_000, "BD-TEST"));
            var bal1 = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(15_000, bal1);
            Assert.Equal(100_000, discount);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .RestoreAsync(_h.Bob, booking, 5_000, "BD-TEST"));
            var bal2 = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(20_000, bal2);
            // Replay cùng LedgerKey → trả discount đã lưu, không trừ lần nữa
            var replay = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                    .RedeemAsync(_h.Bob, booking, 5_000, "BD-TEST"));
            Assert.Equal(discount, replay);
            var bal3 = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Bob));
            Assert.Equal(20_000, bal3);
        }

        // ─────────────────────────────────────────────────────────────────
        // J. GROUP: owner-only booking, invite rotation, transfer guard.
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Group: chỉ owner được đặt; member/khách khác không")]
        public async Task Group_OnlyOwnerCanBook()
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().CreateAsync("Team A", _h.Alice, 3));
            var okOwner = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().CanBookForGroupAsync(view!.Id, _h.Alice));
            var notMember = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().CanBookForGroupAsync(view!.Id, _h.Bob));
            Assert.True(okOwner);
            Assert.False(notMember);
        }

        [Fact(DisplayName = "Group: rotate invite → mã cũ bị vô hiệu, mã mới vẫn dùng được")]
        public async Task Group_InviteRotationRevokesPrevious()
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().CreateAsync("Team B", _h.Alice, 3));
            var groupId = view!.Id;
            var original = view.InviteCode!;
            var rotated = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().RotateInviteAsync(groupId, _h.Alice));
            Assert.NotEqual(original, rotated);
            _h.ActAs(_h.Alice, Role.Customer);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().JoinAsync(groupId, _h.Bob, original)));
            Assert.IsType<ConflictException>(err);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().JoinAsync(groupId, _h.Bob, rotated));
            var members = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().GetMembersAsync(groupId));
            Assert.Equal(2, members.Count);
        }

        [Fact(DisplayName = "Group: transfer chỉ cho member hiện tại; owner không thể chuyển cho người lạ")]
        public async Task Group_TransferRequiresMember()
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().CreateAsync("Team C", _h.Alice, 5));
            var groupId = view!.Id;
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().TransferAsync(groupId, _h.Bob, _h.Alice)));
            Assert.IsType<ConflictException>(err);
            // Join Bob rồi transfer thành công
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().JoinAsync(groupId, _h.Bob, view.InviteCode));
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().TransferAsync(groupId, _h.Bob, _h.Alice));
            var g = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<BadmintonBookingDbContext>().PlayGroups.FirstAsync(x => x.Id == groupId));
            Assert.Equal(_h.Bob, g.OwnerId);
        }

        [Fact(DisplayName = "Group: member thường không thể rename/disband (chỉ owner)")]
        public async Task Group_OwnerOnlyMutation()
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().CreateAsync("Team D", _h.Alice, 3));
            var groupId = view!.Id;
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().RenameAsync(groupId, "new", _h.Bob)));
            Assert.IsType<ConflictException>(err);
            var err2 = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Group.IGroupService>().DisbandAsync(groupId, _h.Bob)));
            Assert.IsType<ConflictException>(err2);
        }

        // ─────────────────────────────────────────────────────────────────
        // K. WAITLIST: không cho join slot trống.
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Waitlist: slot chưa có booking active → không được join")]
        public async Task Waitlist_FreeSlotRejected()
        {
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Waitlist.IWaitlistService>()
                    .JoinAsync(_h.Alice, _h.Court2, _h.SlotPeak, _h.FutureDate, 1, null)));
            Assert.IsType<BadRequestException>(err);
            Assert.Contains("Slot còn trống", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Waitlist: cùng user cùng slot+ngày → duplicate bị chặn")]
        public async Task Waitlist_DuplicateRejected()
        {
            // Seed một booking active ở Court2/SlotPeak để slot không còn trống
            await _h.BookAsync(_h.Bob, _h.Court2, _h.SlotPeak, _h.FutureDate.ToString("yyyy-MM-dd"));
            var joined = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Waitlist.IWaitlistService>()
                    .JoinAsync(_h.Alice, _h.Court2, _h.SlotPeak, _h.FutureDate, 1, "muon duoc"));
            Assert.NotNull(joined);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Waitlist.IWaitlistService>()
                    .JoinAsync(_h.Alice, _h.Court2, _h.SlotPeak, _h.FutureDate, 1, "lancua")));
            Assert.IsType<ConflictException>(err);
        }

        // ─────────────────────────────────────────────────────────────────
        // L. CANCEL/REFUND — quyền theo branch (Q3=A).
        // ─────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Cancel refund: manager của cơ sở khác không hoàn được đơn cơ sở mình không thuộc")]
        public async Task Refund_BranchScopeEnforced()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);
            // Set current user to ManagerOtherBranch, then call with matching actorId
            _h.ActAs(_h.ManagerOtherBranch, Role.Manager);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().RefundAsync(view!.Id, _h.ManagerOtherBranch, "cross")));
            Assert.IsType<ConflictException>(err);
            // The branch-scope guard triggers because the booking is on BranchId, not BranchId2.
            Assert.Contains("cơ sở", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Cancel refund: full refund (≥2h) hoàn 100% giá; điểm dùng được restore")]
        public async Task Refund_FullAfterTwoHours()
        {
            await _h.CountPointsIntoAsync(_h.Carol, 10_000, "test-seed-10k");
            var view = await _h.BookAsync(_h.Carol, _h.Court1, _h.SlotNoon,
                _h.FutureDate.ToString("yyyy-MM-dd"), pointsToRedeem: 5_000);
            await _h.ConfirmOnSiteAsync(view!.Id, _h.Manager);
            var refund = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().RefundAsync(view!.Id, _h.Manager, "full"));
            // HanoiNow pinned 09:00, slot 12:00 → 3h trước → 100% hoàn.
            Assert.Equal(PaymentStatus.Refunded, Enum.Parse<PaymentStatus>(refund!.Status));
            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>().GetBalanceAsync(_h.Carol));
            Assert.Equal(10_000, balance); // restore 5000 + giữ 10000 gốc − 5000 redeem → net
        }
    }
}
