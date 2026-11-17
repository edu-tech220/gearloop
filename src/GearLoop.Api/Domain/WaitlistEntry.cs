namespace GearLoop.Api.Domain;

public class WaitlistEntry
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public AppUser Customer { get; set; } = null!;

    public int EquipmentModelId { get; set; }
    public EquipmentModel EquipmentModel { get; set; } = null!;

    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public DateOnly RequestedStart { get; set; }
    public DateOnly RequestedEnd { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime CreatedAtUtc { get; set; }

    public int? PromotedReservationId { get; set; }
}
