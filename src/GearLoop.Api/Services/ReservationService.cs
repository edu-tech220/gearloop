using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Services;

/// Owns the reservation state machine: Confirmed -> CheckedOut -> Returned, or Confirmed ->
/// Cancelled. Checkout assigns a physical unit under optimistic concurrency (rowversion) with a
/// bounded retry loop, since two staff members can race to claim the same pooled unit.
public class ReservationService : IReservationService
{
    private const int MaxRentalDays = 30;
    private const int MaxCheckoutRetries = 5;

    private readonly GearLoopDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IPricingService _pricing;
    private readonly IClock _clock;

    public ReservationService(
        GearLoopDbContext db,
        IAvailabilityService availability,
        IPricingService pricing,
        IClock clock)
    {
        _db = db;
        _availability = availability;
        _pricing = pricing;
        _clock = clock;
    }

    public async Task<ReservationCreationResult> CreateReservationAsync(
        int customerId,
        int equipmentModelId,
        int branchId,
        DateOnly startDate,
        DateOnly endDate,
        bool joinWaitlistIfUnavailable,
        CancellationToken cancellationToken = default)
    {
        var model = await _db.EquipmentModels.FindAsync(new object?[] { equipmentModelId }, cancellationToken)
            ?? throw new NotFoundException($"Equipment model {equipmentModelId} was not found.");
        var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException($"Branch {branchId} was not found.");
        }

        if (endDate <= startDate)
        {
            throw new ValidationException("End date must be after the start date.");
        }

        if (startDate < _clock.Today)
        {
            throw new ValidationException("Start date cannot be in the past.");
        }

        if (endDate.DayNumber - startDate.DayNumber > MaxRentalDays)
        {
            throw new ValidationException($"Rentals cannot exceed {MaxRentalDays} days.");
        }

        var available = await _availability.GetAvailableUnitCountAsync(
            equipmentModelId, branchId, startDate, endDate, cancellationToken: cancellationToken);

        if (available <= 0)
        {
            if (!joinWaitlistIfUnavailable)
            {
                throw new ConflictException("No units are available for the selected dates. Join the waitlist instead.");
            }

            var entry = new WaitlistEntry
            {
                CustomerId = customerId,
                EquipmentModelId = equipmentModelId,
                BranchId = branchId,
                RequestedStart = startDate,
                RequestedEnd = endDate,
                Status = WaitlistStatus.Waiting,
                CreatedAtUtc = _clock.UtcNow
            };
            _db.WaitlistEntries.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);
            return new ReservationCreationResult(null, entry);
        }

        var reservation = BuildConfirmedReservation(customerId, model, branchId, startDate, endDate);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReservationCreationResult(reservation, null);
    }

    public async Task<Reservation> CancelReservationAsync(
        int reservationId,
        int? requestingCustomerId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations.FindAsync(new object?[] { reservationId }, cancellationToken)
            ?? throw new NotFoundException($"Reservation {reservationId} was not found.");

        if (requestingCustomerId is not null && reservation.CustomerId != requestingCustomerId)
        {
            throw new NotFoundException($"Reservation {reservationId} was not found.");
        }

        if (reservation.Status != ReservationStatus.Confirmed)
        {
            throw new ConflictException($"Cannot cancel a reservation in {reservation.Status} status.");
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await PromoteWaitlistAsync(reservation.EquipmentModelId, reservation.BranchId, cancellationToken);
        return reservation;
    }

    public async Task<Reservation> CheckoutAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxCheckoutRetries; attempt++)
        {
            var reservation = await _db.Reservations
                .Include(r => r.EquipmentModel)
                .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
                ?? throw new NotFoundException($"Reservation {reservationId} was not found.");

            if (reservation.Status != ReservationStatus.Confirmed)
            {
                throw new ConflictException($"Reservation {reservationId} is not awaiting checkout.");
            }

            var unit = await _db.EquipmentUnits
                .Where(u => u.EquipmentModelId == reservation.EquipmentModelId
                            && u.BranchId == reservation.BranchId
                            && u.Status == UnitStatus.Available)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (unit is null)
            {
                throw new ConflictException("No available unit could be assigned for checkout.");
            }

            unit.Status = UnitStatus.CheckedOut;
            reservation.AssignedUnitId = unit.Id;
            reservation.Status = ReservationStatus.CheckedOut;
            reservation.CheckedOutAtUtc = _clock.UtcNow;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return reservation;
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
            }
        }

        throw new ConflictException("Checkout could not complete after several attempts due to concurrent updates. Please retry.");
    }

    public async Task<Reservation> ReturnAsync(
        int reservationId,
        DateOnly actualReturnDate,
        UnitCondition condition,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.EquipmentModel)
            .Include(r => r.Charge)
            .Include(r => r.AssignedUnit)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.Status != ReservationStatus.CheckedOut || reservation.AssignedUnit is null)
        {
            throw new ConflictException($"Reservation {reservationId} is not checked out.");
        }

        var charge = reservation.Charge
            ?? throw new ConflictException($"Reservation {reservationId} has no rental charge on record.");

        var lateFee = _pricing.CalculateLateFee(reservation.EquipmentModel, reservation.EndDate, actualReturnDate);
        var damageFee = _pricing.CalculateDamageFee(reservation.EquipmentModel, charge.DepositAmount, condition);

        charge.LateFeeAmount = lateFee;
        charge.DamageFeeAmount = damageFee;
        charge.DepositRefunded = Math.Max(0, charge.DepositAmount - damageFee);
        charge.TotalAmount = charge.BaseAmount - charge.DiscountAmount + charge.SurchargeAmount + lateFee + damageFee;

        reservation.Status = ReservationStatus.Returned;
        reservation.ActualReturnDate = actualReturnDate;
        reservation.ReturnCondition = condition;
        reservation.ReturnedAtUtc = _clock.UtcNow;

        reservation.AssignedUnit.Condition = condition;
        reservation.AssignedUnit.Status = condition is UnitCondition.Damaged or UnitCondition.Retired
            ? UnitStatus.Maintenance
            : UnitStatus.Available;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The reservation or its unit was modified concurrently. Please retry.");
        }

        await PromoteWaitlistAsync(reservation.EquipmentModelId, reservation.BranchId, cancellationToken);
        return reservation;
    }

    private async Task PromoteWaitlistAsync(int equipmentModelId, int branchId, CancellationToken cancellationToken)
    {
        var pending = await _db.WaitlistEntries
            .Include(w => w.EquipmentModel)
            .Where(w => w.EquipmentModelId == equipmentModelId
                        && w.BranchId == branchId
                        && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var entry in pending)
        {
            var available = await _availability.GetAvailableUnitCountAsync(
                entry.EquipmentModelId, entry.BranchId, entry.RequestedStart, entry.RequestedEnd,
                cancellationToken: cancellationToken);

            if (available <= 0)
            {
                continue;
            }

            var reservation = BuildConfirmedReservation(
                entry.CustomerId, entry.EquipmentModel, entry.BranchId, entry.RequestedStart, entry.RequestedEnd);

            _db.Reservations.Add(reservation);
            entry.Status = WaitlistStatus.Promoted;
            await _db.SaveChangesAsync(cancellationToken);
            entry.PromotedReservationId = reservation.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private Reservation BuildConfirmedReservation(
        int customerId, EquipmentModel model, int branchId, DateOnly startDate, DateOnly endDate)
    {
        var price = _pricing.CalculateRentalPrice(model, startDate, endDate);

        return new Reservation
        {
            CustomerId = customerId,
            EquipmentModelId = model.Id,
            BranchId = branchId,
            StartDate = startDate,
            EndDate = endDate,
            Status = ReservationStatus.Confirmed,
            CreatedAtUtc = _clock.UtcNow,
            Charge = new RentalCharge
            {
                BaseAmount = price.BaseAmount,
                DiscountAmount = price.DiscountAmount,
                SurchargeAmount = price.SurchargeAmount,
                LateFeeAmount = 0,
                DamageFeeAmount = 0,
                DepositAmount = price.DepositAmount,
                DepositRefunded = 0,
                TotalAmount = price.TotalAmount
            }
        };
    }
}
