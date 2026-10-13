using Contract.Repositories.Entity;
using M.Core.Enums;
using M.Core.Utils;
using Repositories.Context;

namespace BadmintonBE.API
{
    /// <summary>
    /// Idempotent seed: only inserts when the table is empty, safe to run on
    /// every boot. Roles/CourtTypes/TimeSlots/Courts are seeded here; no
    /// passwords are ever seeded (the API creates users at runtime).
    /// </summary>
    public static class SeedData
    {
        public static void Seed(BadmintonBookingDbContext db)
        {
            // ---- Roles ----
            if (!db.Roles.Any())
            {
                // RoleId is SQL IDENTITY; let the DB assign it (do NOT set
                // explicit values, or EF inserts them and triggers the
                // IDENTITY_INSERT error).
                db.Roles.AddRange(
                    new Role { Name = "Admin", Description = "Full system administrator", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Role { Name = "Staff", Description = "Badminton court staff", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Role { Name = "Customer", Description = "Customer who books courts", IsActive = true, CreatedAt = DateTime.UtcNow });
                db.SaveChanges();
            }

            // ---- Court types ----
            if (!db.CourtTypes.Any())
            {
                db.CourtTypes.AddRange(
                    new CourtType { Name = "Standard", Description = "Standard badminton court", IsActive = true },
                    new CourtType { Name = "VIP", Description = "VIP badminton court", IsActive = true },
                    new CourtType { Name = "Premium", Description = "Premium badminton court", IsActive = true });
                db.SaveChanges();
            }

            var standard = db.CourtTypes.First(t => t.Name == "Standard");
            var vip = db.CourtTypes.First(t => t.Name == "VIP");
            var premium = db.CourtTypes.First(t => t.Name == "Premium");

            // ---- Time slots (06:00-23:00, hourly) ----
            if (!db.TimeSlots.Any())
            {
                var slots = new List<TimeSlot>();
                for (var h = 6; h < 23; h++)
                    slots.Add(new TimeSlot
                    {
                        StartTime = new TimeOnly(h, 0),
                        EndTime = new TimeOnly(h + 1, 0),
                        IsActive = true
                    });
                db.TimeSlots.AddRange(slots);
                db.SaveChanges();
            }

            // ---- Courts ----
            if (!db.Courts.Any())
            {
                db.Courts.AddRange(
                    new Court { CourtTypeId = standard.Id, CourtCode = "STD-01", CourtName = "Sân Standard 01", Location = "Tầng 1", PricePerHour = 80000, Status = CourtStatus.Available, IsActive = true },
                    new Court { CourtTypeId = standard.Id, CourtCode = "STD-02", CourtName = "Sân Standard 02", Location = "Tầng 1", PricePerHour = 80000, Status = CourtStatus.Available, IsActive = true },
                    new Court { CourtTypeId = standard.Id, CourtCode = "STD-03", CourtName = "Sân Standard 03", Location = "Tầng 1", PricePerHour = 80000, Status = CourtStatus.Available, IsActive = true },
                    new Court { CourtTypeId = vip.Id, CourtCode = "VIP-01", CourtName = "Sân VIP 01", Location = "Tầng 2", PricePerHour = 120000, Status = CourtStatus.Available, IsActive = true },
                    new Court { CourtTypeId = vip.Id, CourtCode = "VIP-02", CourtName = "Sân VIP 02", Location = "Tầng 2", PricePerHour = 120000, Status = CourtStatus.Available, IsActive = true },
                    new Court { CourtTypeId = premium.Id, CourtCode = "PRE-01", CourtName = "Sân Premium 01", Location = "Tầng 2", PricePerHour = 150000, Status = CourtStatus.Available, IsActive = true },
                    new Court { CourtTypeId = premium.Id, CourtCode = "PRE-02", CourtName = "Sân Premium 02", Location = "Tầng 2", PricePerHour = 150000, Status = CourtStatus.Available, IsActive = true });
                db.SaveChanges();
            }
        }
    }
}
