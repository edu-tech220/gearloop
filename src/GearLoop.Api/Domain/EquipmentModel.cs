namespace GearLoop.Api.Domain;

public class EquipmentModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal ReplacementValue { get; set; }

    public ICollection<EquipmentUnit> Units { get; set; } = new List<EquipmentUnit>();
}
