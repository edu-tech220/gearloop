namespace GearLoop.Api.Domain;

public class Reservation
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public AppUser Customer { get; set; } = null!;

    public int EquipmentModelId { get; set; }
    public EquipmentModel EquipmentModel { get; set; } = null!;

    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;

    public int? AssignedUnitId { get; set; }
    public EquipmentUnit? AssignedUnit { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CheckedOutAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public DateOnly? ActualReturnDate { get; set; }
    public UnitCondition? ReturnCondition { get; set; }

    public RentalCharge? Charge { get; set; }

    /// Optimistic concurrency token guarding against duplicate/concurrent state transitions.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
