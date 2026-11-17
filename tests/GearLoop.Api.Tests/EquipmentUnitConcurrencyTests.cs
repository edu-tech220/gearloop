using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GearLoop.Api.Tests;

/// Verifies the app-managed concurrency token itself (independent of ReservationService) so a
/// regression in the EF Core model configuration - e.g. someone removing IsConcurrencyToken() -
/// would be caught even though SQLite can't reproduce the SQL Server rowversion machinery.
public class EquipmentUnitConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EquipmentUnitConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private GearLoopDbContext NewContext() =>
        new(new DbContextOptionsBuilder<GearLoopDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task TwoContextsLoadingTheSameUnit_SecondSaveAfterFirstCommits_ThrowsConcurrencyException()
    {
        int unitId;
        using (var seedDb = NewContext())
        {
            var branch = new Branch { Name = "Downtown", City = "Metropolis", Address = "1 Main St" };
            var model = new EquipmentModel
            {
                Name = "Drone", Category = "Photography", Description = "4k drone",
                DailyRate = 55m, DepositAmount = 400m, ReplacementValue = 1600m
            };
            var unit = new EquipmentUnit { EquipmentModel = model, Branch = branch, SerialNumber = "DRN-001" };
            seedDb.Branches.Add(branch);
            seedDb.EquipmentModels.Add(model);
            seedDb.EquipmentUnits.Add(unit);
            await seedDb.SaveChangesAsync();
            unitId = unit.Id;
        }

        // Two independent request-scoped contexts both read the unit while it is still Available.
        using var contextA = NewContext();
        using var contextB = NewContext();

        var unitSeenByA = await contextA.EquipmentUnits.FindAsync(unitId);
        var unitSeenByB = await contextB.EquipmentUnits.FindAsync(unitId);

        // Context A "wins the race": claims the unit and commits first.
        unitSeenByA!.Status = UnitStatus.CheckedOut;
        unitSeenByA.RowVersion = Guid.NewGuid().ToByteArray();
        await contextA.SaveChangesAsync();

        // Context B still holds the pre-claim RowVersion, so its write must be rejected rather
        // than silently overwriting A's change.
        unitSeenByB!.Status = UnitStatus.CheckedOut;
        unitSeenByB.RowVersion = Guid.NewGuid().ToByteArray();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
    }
}
