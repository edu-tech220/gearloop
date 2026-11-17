using GearLoop.Api.Domain;
using GearLoop.Api.Security;

namespace GearLoop.Api.Data;

public static class DbSeeder
{
    public const string DemoAdminEmail = "admin@gearloop.dev";
    public const string DemoAdminPassword = "Admin#2024";
    public const string DemoStaffEmail = "staff@gearloop.dev";
    public const string DemoStaffPassword = "Staff#2024";

    public static void Seed(GearLoopDbContext db)
    {
        if (!db.Users.Any())
        {
            db.Users.Add(new AppUser
            {
                FullName = "GearLoop Admin",
                Email = DemoAdminEmail,
                PasswordHash = PasswordHasher.Hash(DemoAdminPassword),
                Role = UserRole.Admin,
                CreatedAtUtc = DateTime.UtcNow
            });
            db.Users.Add(new AppUser
            {
                FullName = "Branch Staff",
                Email = DemoStaffEmail,
                PasswordHash = PasswordHasher.Hash(DemoStaffPassword),
                Role = UserRole.Staff,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (!db.Branches.Any())
        {
            db.Branches.AddRange(
                new Branch { Name = "GearLoop Downtown", City = "Mexico City", Address = "Av. Reforma 123" },
                new Branch { Name = "GearLoop North", City = "Monterrey", Address = "Av. Constitución 456" });
        }

        db.SaveChanges();

        if (!db.EquipmentModels.Any())
        {
            var downtown = db.Branches.First(b => b.Name == "GearLoop Downtown");
            var north = db.Branches.First(b => b.Name == "GearLoop North");

            var camera = new EquipmentModel
            {
                Name = "Mirrorless Camera Kit",
                Category = "Photography",
                Description = "Full-frame mirrorless camera with a 24-70mm lens.",
                DailyRate = 45.00m,
                DepositAmount = 300.00m,
                ReplacementValue = 2200.00m
            };
            var tent = new EquipmentModel
            {
                Name = "4-Person Camping Tent",
                Category = "Outdoor",
                Description = "Weatherproof tent with a 10-minute setup.",
                DailyRate = 18.00m,
                DepositAmount = 60.00m,
                ReplacementValue = 350.00m
            };
            var drone = new EquipmentModel
            {
                Name = "4K Aerial Drone",
                Category = "Photography",
                Description = "Compact drone with a 3-axis gimbal and 30 min flight time.",
                DailyRate = 55.00m,
                DepositAmount = 400.00m,
                ReplacementValue = 1600.00m
            };

            db.EquipmentModels.AddRange(camera, tent, drone);
            db.SaveChanges();

            db.EquipmentUnits.AddRange(
                new EquipmentUnit { EquipmentModel = camera, Branch = downtown, SerialNumber = "CAM-DT-001" },
                new EquipmentUnit { EquipmentModel = camera, Branch = downtown, SerialNumber = "CAM-DT-002" },
                new EquipmentUnit { EquipmentModel = camera, Branch = north, SerialNumber = "CAM-NO-001" },
                new EquipmentUnit { EquipmentModel = tent, Branch = downtown, SerialNumber = "TEN-DT-001" },
                new EquipmentUnit { EquipmentModel = tent, Branch = downtown, SerialNumber = "TEN-DT-002" },
                new EquipmentUnit { EquipmentModel = tent, Branch = downtown, SerialNumber = "TEN-DT-003" },
                new EquipmentUnit { EquipmentModel = tent, Branch = north, SerialNumber = "TEN-NO-001" },
                new EquipmentUnit { EquipmentModel = drone, Branch = downtown, SerialNumber = "DRN-DT-001" },
                new EquipmentUnit { EquipmentModel = drone, Branch = north, SerialNumber = "DRN-NO-001" });

            db.SaveChanges();
        }
    }
}
