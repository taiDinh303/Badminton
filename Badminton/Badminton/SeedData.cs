using BCrypt.Net;
using Contract.Repositories.Entity;
using M.Core.Enums;
using Repositories.Context;

namespace BadmintonBE.API
{
    public static class SeedData
    {
        public static void Seed(BadmintonBookingDbContext db)
        {
            if (db.Branches.Any() || db.Users.Any())
                return; // idempotent

            var branch = new Branch
            {
                Name = "Sân Cầu Lông Minh Long",
                Address = "123 Nguyễn Văn A, Hà Nội",
                OpenTime = new TimeOnly(6, 0),
                CloseTime = new TimeOnly(23, 0),
                Status = BranchStatus.Active
            };
            db.Branches.Add(branch);

            //17 one-hour slots, 06:00 → 22:00; peak = start ≥18:00.
            var slots = new List<TimeSlot>();
            for (var h = 6; h < 23; h++)
            {
                var start = new TimeOnly(h, 0);
                var end = new TimeOnly(h + 1, 0);
                slots.Add(new TimeSlot
                {
                    Label = $"{h:00}:00",
                    StartTime = start,
                    EndTime = end,
                    IsPeak = h >= 18,
                    IsActive = true
                });
            }
            db.TimeSlots.AddRange(slots);

            db.SaveChanges();

            db.Courts.AddRange(
                new Court
                {
                    BranchId = branch.Id,
                    Code = "S01",
                    Name = "Sân 1 (Thường)",
                    Surface = CourtSurface.Sindalo,
                    PriceLowPeak = 100_000,
                    PricePeak = 150_000,
                    WeekendSurcharge = 0.20m,
                    Capacity = 2,
                    IsActive = true
                },
                new Court
                {
                    BranchId = branch.Id,
                    Code = "S02",
                    Name = "Sân 2 (Cao cấp)",
                    Surface = CourtSurface.Carpet,
                    PriceLowPeak = 150_000,
                    PricePeak = 220_000,
                    WeekendSurcharge = 0.30m,
                    Capacity = 4,
                    Amenities = "[\"máy lạnh\",\"giếng mát\"]",
                    IsActive = true
                },
                new Court
                {
                    BranchId = branch.Id,
                    Code = "S03",
                    Name = "Sân 3 (Tre)",
                    Surface = CourtSurface.Wood,
                    PriceLowPeak = 120_000,
                    PricePeak = 180_000,
                    WeekendSurcharge = 0.20m,
                    Capacity = 2,
                    IsActive = true
                });

            db.Users.Add(new User
            {
                FullName = "Quản trị viên",
                PhoneNumber = "0900000001",
                Email = "admin@badminton.dev",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = Role.Admin,
                Status = UserStatus.Active,
                IsVerified = true
            });

            db.SaveChanges();
        }
    }
}
