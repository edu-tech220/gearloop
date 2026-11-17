namespace GearLoop.Api.Domain;

public class Branch
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public ICollection<EquipmentUnit> Units { get; set; } = new List<EquipmentUnit>();
}
