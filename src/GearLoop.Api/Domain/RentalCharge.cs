namespace GearLoop.Api.Domain;

public class RentalCharge
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public decimal BaseAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal LateFeeAmount { get; set; }
    public decimal DamageFeeAmount { get; set; }

    public decimal DepositAmount { get; set; }
    public decimal DepositRefunded { get; set; }

    public decimal TotalAmount { get; set; }
}
