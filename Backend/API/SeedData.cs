using BCrypt.Net;
using Contract.Repositories.Entity;
using Badminton.Core.Enums;
using Repositories.Context;

namespace Badminton.API
{
    public static class SeedData
    {
        public static void Seed(BadmintonBookingDbContext db)
        {
            if (db.Branches.Any() || db.Users.Any())
                return; // idempotent

            // ── 3 branches ──
            var b1 = new Branch
            {
                Name = "Sân Cầu Lông Minh Long (Hà Đông)",
                Address = "123 Nguyễn Văn A, Hà Đông, Hà Nội",
                OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(23, 0),
                Status = BranchStatus.Active
            };
            var b2 = new Branch
            {
                Name = "Sân Cầu Lông Minh Long (Cầu Giấy)",
                Address = "45 Trần Duy Hưng, Cầu Giấy, Hà Nội",
                OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(23, 0),
                Status = BranchStatus.Active
            };
            var b3 = new Branch
            {
                Name = "Sân Cầu Lông Minh Long (Thịnh Vỹ)",
                Address = "Thịnh Vỹ, Phú Quốc, Kiên Giang",
                OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0),
                Status = BranchStatus.Closed // closed branch for testing filter
            };
            db.Branches.AddRange(b1, b2, b3);

            // ── 17 one-hour slots, 06:00 → 22:00; peak = start ≥ 18:00 ──
            var slots = new List<TimeSlot>();
            for (var h = 6; h < 23; h++)
                slots.Add(new TimeSlot
                {
                    Label = $"{h:00}:00",
                    StartTime = new TimeOnly(h, 0),
                    EndTime = new TimeOnly(h + 1, 0),
                    IsPeak = h >= 18,
                    IsActive = true
                });
            db.TimeSlots.AddRange(slots);
            db.SaveChanges();

            // ── 9 courts across 2 active branches ──
            db.Courts.AddRange(
                // Hà Đông
                NewCourt(b1.Id, "S01", "Sân 1 (Thường)", CourtSurface.Sindalo, 100_000, 150_000, 0.20m, 2),
                NewCourt(b1.Id, "S02", "Sân 2 (Cao cấp)", CourtSurface.Carpet, 150_000, 220_000, 0.30m, 4, "[\"máy lạnh\",\"giếng mát\"]"),
                NewCourt(b1.Id, "S03", "Sân 3 (Tre)", CourtSurface.Wood, 120_000, 180_000, 0.20m, 2),
                // Cầu Giấy
                NewCourt(b2.Id, "S01", "Sân 1 (Sân trong)", CourtSurface.Carpet, 180_000, 260_000, 0.25m, 2, "[\"trần cao\",\"đèn LED\"]"),
                NewCourt(b2.Id, "S02", "Sân 2 (Tre cao cấp)", CourtSurface.Wood, 200_000, 280_000, 0.25m, 4),
                NewCourt(b2.Id, "S03", "Sân 3 (Sindalo)", CourtSurface.Sindalo, 150_000, 210_000, 0.20m, 2));
            db.SaveChanges();

            var courts = db.Courts.ToList();

            // ── Voucher ──
            db.Vouchers.AddRange(
                new Voucher
                {
                    Code = "WELCOME10", Type = VoucherType.Percent, Value = 10,
                    MinSpend = 300_000, TotalQty = 1000, UsedQty = 0,
                    ValidFrom = DateOnly.FromDateTime(DateTime.Now.AddDays(-30)),
                    ValidUntil = DateOnly.FromDateTime(DateTime.Now.AddDays(365)),
                    IsActive = true
                },
                new Voucher
                {
                    Code = "DGM20K", Type = VoucherType.Fixed, Value = 20_000,
                    MinSpend = 200_000, TotalQty = 500, UsedQty = 0,
                    ValidFrom = DateOnly.FromDateTime(DateTime.Now.AddDays(-30)),
                    ValidUntil = DateOnly.FromDateTime(DateTime.Now.AddDays(90)),
                    IsActive = true
                });

            // ── Admin + 3 managers (1 per branch) ──
            var admin = new User
            {
                FullName = "Quản trị viên",
                PhoneNumber = "0900000001",
                Email = "admin@badminton.dev",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = Role.Admin, Status = UserStatus.Active, IsVerified = true
            };
            var m1 = StaffUser("Trương Minh Long", "0900000002", b1.Id, Role.Manager);
            var m2 = StaffUser("Ngô Văn Cường", "0900000003", b2.Id, Role.Manager);
            var m3 = StaffUser("Lê Thu Hà", "0900000004", b2.Id, Role.Manager);
            db.Users.AddRange(admin, m1, m2, m3);

            // ── 500 customers (hashed password 1 call + random fields) ──
            var rng = new Random(20261015);
            var firstNames = new[] { "Minh", "Hùng", "Linh", "An", "Tú", "Hải", "Tuấn", "Hương", "Nhật", "Trần" };
            var lastNames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Vũ", "Đặng", "Bùi", "Đỗ", "Đinh" };
            var customers = new List<User>();
            for (int i = 0; i < 500; i++)
            {
                var fn = firstNames[rng.Next(firstNames.Length)];
                var ln = lastNames[rng.Next(lastNames.Length)];
                customers.Add(new User
                {
                    FullName = $"{ln} {fn} {i + 1}",
                    PhoneNumber = $"091{i:D7}",
                    Email = $"user{i + 1}@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
                    Role = Role.Customer,
                    BranchId = rng.NextDouble() < 0.5 ? b1.Id : b2.Id,
                    Status = UserStatus.Active,
                    IsVerified = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(90))
                });
            }
            db.Users.AddRange(customers);
            db.SaveChanges();

            var slotIds = db.TimeSlots.Select(s => s.Id).ToList();
            var statuses = new[] { BookingStatus.Confirmed, BookingStatus.Completed,
                BookingStatus.Cancelled, BookingStatus.NoShow };
            var users = db.Users.Where(u => u.Role == Role.Customer).ToList();
            var now = DateTime.UtcNow;
            var used = new HashSet<string>();
            var bookings = new List<Booking>();
            int target = 300;
            int attempts = 0;
            while (bookings.Count < target && attempts < 5000)
            {
                attempts++;
                var court = courts[rng.Next(courts.Count)];
                var slotId = slotIds[rng.Next(slotIds.Count)];
                var daysAgo = rng.Next(30);
                var date = DateOnly.FromDateTime(now.AddDays(-daysAgo));
                var key = $"{court.Id}|{slotId}|{date}";
                if (!used.Add(key)) continue;
                var st = statuses[rng.Next(statuses.Length)];
                var user = users[rng.Next(users.Count)];
                var price = st == BookingStatus.NoShow ? 0 : (rng.NextDouble() < 0.4 ? court.PricePeak : court.PriceLowPeak);
                bookings.Add(new Booking
                {
                    BookingCode = $"BK{10000 + bookings.Count:000}",
                    UserId = user.Id,
                    CourtId = court.Id,
                    SlotId = slotId,
                    BranchId = court.BranchId,
                    Date = date,
                    PlayerCount = 2,
                    Status = st,
                    TotalAmount = price * 2,
                    DiscountAmount = rng.NextDouble() < 0.2 ? 20_000 : 0,
                    PayableAmount = price * 2 - (rng.NextDouble() < 0.2 ? 20_000 : 0),
                    PointsEarned = 0,
                    CreatedAt = now.AddDays(-daysAgo)
                });
            }
            db.Bookings.AddRange(bookings);
            db.SaveChanges();
        }

        private static User StaffUser(string name, string phone, int branchId, Role role) => new()
        {
            FullName = name, PhoneNumber = phone,
            Email = $"{phone}@badminton.dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = role, BranchId = branchId,
            Status = UserStatus.Active, IsVerified = true
        };

        private static Court NewCourt(int branchId, string code, string name,
            CourtSurface surface, int low, int peak, decimal surcharge, int cap,
            string? amenities = null) => new Court
        {
            BranchId = branchId, Code = code, Name = name,
            Surface = surface, PriceLowPeak = low, PricePeak = peak,
            WeekendSurcharge = surcharge, Capacity = cap,
            Amenities = amenities, IsActive = true
        };
    }
}
