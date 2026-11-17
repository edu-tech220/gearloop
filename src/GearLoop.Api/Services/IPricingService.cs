using GearLoop.Api.Domain;

namespace GearLoop.Api.Services;

public record RentalPrice(
    int RentalDays,
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal SurchargeAmount,
    decimal TotalAmount,
    decimal DepositAmount);

public interface IPricingService
{
    RentalPrice CalculateRentalPrice(EquipmentModel model, DateOnly startDate, DateOnly endDate);

    decimal CalculateLateFee(EquipmentModel model, DateOnly dueDate, DateOnly actualReturnDate);

    decimal CalculateDamageFee(EquipmentModel model, decimal depositAmount, UnitCondition reportedCondition);
}
