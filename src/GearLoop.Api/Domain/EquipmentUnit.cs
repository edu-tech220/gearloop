namespace GearLoop.Api.Domain;

public class EquipmentUnit
{
    public int Id { get; set; }
    public int EquipmentModelId { get; set; }
    public EquipmentModel EquipmentModel { get; set; } = null!;

    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public string SerialNumber { get; set; } = string.Empty;
    public UnitCondition Condition { get; set; } = UnitCondition.Good;
    public UnitStatus Status { get; set; } = UnitStatus.Available;

    /// Optimistic concurrency token: guards unit assignment during checkout races.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
