using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GearLoop.Api.Tests;

public class AvailabilityServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GearLoopDbContext _db;
    private readonly AvailabilityService _sut;

    public AvailabilityServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GearLoopDbContext>().UseSqlite(_connection).Options;
        _db = new GearLoopDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new AvailabilityService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private (Branch Branch, EquipmentModel Model) SeedModelWithUnits(int unitCount)
    {
        var branch = new Branch { Name = "Downtown", City = "Metropolis", Address = "1 Main St" };
        var model = new EquipmentModel
        {
            Name = "Tent", Category = "Outdoor", Description = "4p tent",
            DailyRate = 10m, DepositAmount = 20m, ReplacementValue = 100m
        };
        _db.Branches.Add(branch);
        _db.EquipmentModels.Add(model);
        _db.SaveChanges();

        for (var i = 0; i < unitCount; i++)
        {
            _db.EquipmentUnits.Add(new EquipmentUnit
            {
                EquipmentModelId = model.Id, BranchId = branch.Id, SerialNumber = $"SER-{i}"
            });
        }

        _db.SaveChanges();
        return (branch, model);
    }

    private int AddCustomer()
    {
        var customer = new AppUser
        {
            FullName = "Jane Doe", Email = $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash", Role = UserRole.Customer, CreatedAtUtc = DateTime.UtcNow
        };
        _db.Users.Add(customer);
        _db.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task GetAvailableUnitCountAsync_NoReservations_ReturnsTotalRentableUnits()
    {
        var (branch, model) = SeedModelWithUnits(3);

        var available = await _sut.GetAvailableUnitCountAsync(
            model.Id, branch.Id, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 5));

        Assert.Equal(3, available);
    }

    [Fact]
    public async Task GetAvailableUnitCountAsync_OverlappingActiveReservation_ReducesCapacity()
    {
        var (branch, model) = SeedModelWithUnits(2);
        var customerId = AddCustomer();

        _db.Reservations.Add(new Reservation
        {
            CustomerId = customerId, EquipmentModelId = model.Id, BranchId = branch.Id,
            StartDate = new DateOnly(2030, 1, 1), EndDate = new DateOnly(2030, 1, 5),
            Status = ReservationStatus.Confirmed, CreatedAtUtc = DateTime.UtcNow
        });
        _db.SaveChanges();

        var available = await _sut.GetAvailableUnitCountAsync(
            model.Id, branch.Id, new DateOnly(2030, 1, 2), new DateOnly(2030, 1, 3));

        Assert.Equal(1, available);
    }

    [Fact]
    public async Task GetAvailableUnitCountAsync_NonOverlappingDateRange_DoesNotReduceCapacity()
    {
        var (branch, model) = SeedModelWithUnits(2);
        var customerId = AddCustomer();

        _db.Reservations.Add(new Reservation
        {
            CustomerId = customerId, EquipmentModelId = model.Id, BranchId = branch.Id,
            StartDate = new DateOnly(2030, 1, 1), EndDate = new DateOnly(2030, 1, 5),
            Status = ReservationStatus.Confirmed, CreatedAtUtc = DateTime.UtcNow
        });
        _db.SaveChanges();

        var available = await _sut.GetAvailableUnitCountAsync(
            model.Id, branch.Id, new DateOnly(2030, 1, 5), new DateOnly(2030, 1, 8));

        Assert.Equal(2, available);
    }

    [Fact]
    public async Task GetAvailableUnitCountAsync_CancelledReservation_DoesNotReduceCapacity()
    {
        var (branch, model) = SeedModelWithUnits(2);
        var customerId = AddCustomer();

        _db.Reservations.Add(new Reservation
        {
            CustomerId = customerId, EquipmentModelId = model.Id, BranchId = branch.Id,
            StartDate = new DateOnly(2030, 1, 1), EndDate = new DateOnly(2030, 1, 5),
            Status = ReservationStatus.Cancelled, CreatedAtUtc = DateTime.UtcNow
        });
        _db.SaveChanges();

        var available = await _sut.GetAvailableUnitCountAsync(
            model.Id, branch.Id, new DateOnly(2030, 1, 2), new DateOnly(2030, 1, 3));

        Assert.Equal(2, available);
    }

    [Fact]
    public async Task GetAvailableUnitCountAsync_MaintenanceUnit_IsExcludedFromThePool()
    {
        var (branch, model) = SeedModelWithUnits(2);
        var unit = _db.EquipmentUnits.First(u => u.EquipmentModelId == model.Id);
        unit.Status = UnitStatus.Maintenance;
        _db.SaveChanges();

        var available = await _sut.GetAvailableUnitCountAsync(
            model.Id, branch.Id, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 5));

        Assert.Equal(1, available);
    }
}
