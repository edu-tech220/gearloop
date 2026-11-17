using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Services;
using GearLoop.Api.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GearLoop.Api.Tests;

public class ReservationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GearLoopDbContext _db;
    private readonly FixedClock _clock;
    private readonly ReservationService _sut;

    private Branch _branch = null!;
    private EquipmentModel _model = null!;

    public ReservationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GearLoopDbContext>().UseSqlite(_connection).Options;
        _db = new GearLoopDbContext(options);
        _db.Database.EnsureCreated();

        _clock = new FixedClock(new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        _sut = new ReservationService(_db, new AvailabilityService(_db), new PricingService(), _clock);

        SeedBranchAndModel();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SeedBranchAndModel(int unitCount = 1)
    {
        _branch = new Branch { Name = "Downtown", City = "Metropolis", Address = "1 Main St" };
        _model = new EquipmentModel
        {
            Name = "Camera", Category = "Photography", Description = "Mirrorless kit",
            DailyRate = 50m, DepositAmount = 200m, ReplacementValue = 1500m
        };
        _db.Branches.Add(_branch);
        _db.EquipmentModels.Add(_model);
        _db.SaveChanges();

        for (var i = 0; i < unitCount; i++)
        {
            _db.EquipmentUnits.Add(new EquipmentUnit { EquipmentModelId = _model.Id, BranchId = _branch.Id, SerialNumber = $"SER-{i}" });
        }

        _db.SaveChanges();
    }

    private int AddCustomer(string? email = null)
    {
        var customer = new AppUser
        {
            FullName = "Test Customer", Email = email ?? $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash", Role = UserRole.Customer, CreatedAtUtc = DateTime.UtcNow
        };
        _db.Users.Add(customer);
        _db.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task CreateReservationAsync_WithAvailability_CreatesConfirmedReservationWithCharge()
    {
        var customerId = AddCustomer();

        var result = await _sut.CreateReservationAsync(
            customerId, _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(4), joinWaitlistIfUnavailable: false);

        Assert.NotNull(result.Reservation);
        Assert.Null(result.WaitlistEntry);
        Assert.Equal(ReservationStatus.Confirmed, result.Reservation!.Status);
        Assert.NotNull(result.Reservation.Charge);
        Assert.Equal(150m, result.Reservation.Charge!.BaseAmount); // 50/day * 3 days
    }

    [Fact]
    public async Task CreateReservationAsync_NoAvailabilityWithoutWaitlistOptIn_ThrowsConflict()
    {
        var customerId = AddCustomer();
        await _sut.CreateReservationAsync(customerId, _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(4), false);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(2), _clock.Today.AddDays(3), false));
    }

    [Fact]
    public async Task CreateReservationAsync_NoAvailabilityWithWaitlistOptIn_CreatesWaitlistEntry()
    {
        var firstCustomer = AddCustomer();
        await _sut.CreateReservationAsync(firstCustomer, _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(4), false);

        var result = await _sut.CreateReservationAsync(
            AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(2), _clock.Today.AddDays(3), joinWaitlistIfUnavailable: true);

        Assert.Null(result.Reservation);
        Assert.NotNull(result.WaitlistEntry);
        Assert.Equal(WaitlistStatus.Waiting, result.WaitlistEntry!.Status);
    }

    [Fact]
    public async Task CreateReservationAsync_StartDateInThePast_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(-1), _clock.Today.AddDays(2), false));
    }

    [Fact]
    public async Task CreateReservationAsync_RentalLongerThanMax_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today, _clock.Today.AddDays(31), false));
    }

    [Fact]
    public async Task CheckoutAsync_WithAvailableUnit_AssignsUnitAndSetsCheckedOut()
    {
        var result = await _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(2), false);

        var reservation = await _sut.CheckoutAsync(result.Reservation!.Id);

        Assert.Equal(ReservationStatus.CheckedOut, reservation.Status);
        Assert.NotNull(reservation.AssignedUnitId);

        var unit = await _db.EquipmentUnits.FindAsync(reservation.AssignedUnitId);
        Assert.Equal(UnitStatus.CheckedOut, unit!.Status);
    }

    [Fact]
    public async Task CheckoutAsync_WhenTheOnlyUnitIsAlreadyAssigned_ThrowsConflictAndDoesNotDoubleAssign()
    {
        var first = await _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(2), false);
        var second = await _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(5), _clock.Today.AddDays(6), false);

        await _sut.CheckoutAsync(first.Reservation!.Id);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CheckoutAsync(second.Reservation!.Id));

        var unitsCheckedOut = await _db.EquipmentUnits.CountAsync(u => u.Status == UnitStatus.CheckedOut);
        Assert.Equal(1, unitsCheckedOut);
    }

    [Fact]
    public async Task ReturnAsync_OnTimeInGoodCondition_ChargesNoFeesAndFreesTheUnit()
    {
        var created = await _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(3), false);
        var checkedOut = await _sut.CheckoutAsync(created.Reservation!.Id);

        var returned = await _sut.ReturnAsync(checkedOut.Id, _clock.Today.AddDays(3), UnitCondition.Good);

        Assert.Equal(ReservationStatus.Returned, returned.Status);
        Assert.Equal(0m, returned.Charge!.LateFeeAmount);
        Assert.Equal(0m, returned.Charge.DamageFeeAmount);
        Assert.Equal(returned.Charge.DepositAmount, returned.Charge.DepositRefunded);

        var unit = await _db.EquipmentUnits.FindAsync(returned.AssignedUnitId);
        Assert.Equal(UnitStatus.Available, unit!.Status);
    }

    [Fact]
    public async Task ReturnAsync_LateAndDamaged_AppliesLateAndDamageFeesAndSendsUnitToMaintenance()
    {
        var created = await _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(3), false);
        var checkedOut = await _sut.CheckoutAsync(created.Reservation!.Id);

        var returned = await _sut.ReturnAsync(checkedOut.Id, _clock.Today.AddDays(5), UnitCondition.Damaged);

        Assert.Equal(150m, returned.Charge!.LateFeeAmount); // 50 * 1.5 * 2 days late
        Assert.Equal(150m, returned.Charge.DamageFeeAmount); // 75% of 200 deposit
        Assert.Equal(50m, returned.Charge.DepositRefunded);

        var unit = await _db.EquipmentUnits.FindAsync(returned.AssignedUnitId);
        Assert.Equal(UnitStatus.Maintenance, unit!.Status);
    }

    [Fact]
    public async Task CancelReservationAsync_FreesCapacity_PromotesOldestWaitlistEntry()
    {
        var holder = await _sut.CreateReservationAsync(AddCustomer(), _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(4), false);

        var waitlisted = await _sut.CreateReservationAsync(
            AddCustomer("waiter@test.local"), _model.Id, _branch.Id, _clock.Today.AddDays(2), _clock.Today.AddDays(3), joinWaitlistIfUnavailable: true);
        Assert.NotNull(waitlisted.WaitlistEntry);

        await _sut.CancelReservationAsync(holder.Reservation!.Id, requestingCustomerId: null);

        var promotedEntry = await _db.WaitlistEntries.FindAsync(waitlisted.WaitlistEntry!.Id);
        Assert.Equal(WaitlistStatus.Promoted, promotedEntry!.Status);
        Assert.NotNull(promotedEntry.PromotedReservationId);

        var newReservation = await _db.Reservations.FindAsync(promotedEntry.PromotedReservationId);
        Assert.Equal(ReservationStatus.Confirmed, newReservation!.Status);
    }

    [Fact]
    public async Task CancelReservationAsync_WrongCustomer_ThrowsNotFound()
    {
        var owner = AddCustomer();
        var created = await _sut.CreateReservationAsync(owner, _model.Id, _branch.Id, _clock.Today.AddDays(1), _clock.Today.AddDays(2), false);

        var someoneElse = AddCustomer();
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CancelReservationAsync(created.Reservation!.Id, someoneElse));
    }
}
