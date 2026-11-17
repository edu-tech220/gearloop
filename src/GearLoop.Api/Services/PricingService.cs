using GearLoop.Api.Domain;

namespace GearLoop.Api.Services;

/// Pricing rules for a rental:
/// - multi-day discount tiers reward longer rentals (3+ days: 10%, 7+ days: 15%)
/// - a weekend surcharge (10% of the daily rate per Saturday/Sunday covered) reflects higher demand
/// - a late return is penalized at 1.5x the daily rate per day overdue
/// - damage on return forfeits a fraction of the deposit proportional to severity
public class PricingService : IPricingService
{
    private const decimal LongRentalDiscount = 0.15m;
    private const decimal ShortRentalDiscount = 0.10m;
    private const int LongRentalThresholdDays = 7;
    private const int ShortRentalThresholdDays = 3;
    private const decimal WeekendSurchargeRate = 0.10m;
    private const decimal LateFeeMultiplier = 1.5m;

    public RentalPrice CalculateRentalPrice(EquipmentModel model, DateOnly startDate, DateOnly endDate)
    {
        var rentalDays = Math.Max(1, endDate.DayNumber - startDate.DayNumber);
        var baseAmount = model.DailyRate * rentalDays;

        var discountRate = rentalDays >= LongRentalThresholdDays
            ? LongRentalDiscount
            : rentalDays >= ShortRentalThresholdDays
                ? ShortRentalDiscount
                : 0m;
        var discountAmount = Math.Round(baseAmount * discountRate, 2);

        var weekendDays = CountWeekendDays(startDate, endDate);
        var surchargeAmount = Math.Round(model.DailyRate * WeekendSurchargeRate * weekendDays, 2);

        var totalAmount = baseAmount - discountAmount + surchargeAmount;

        return new RentalPrice(
            RentalDays: rentalDays,
            BaseAmount: baseAmount,
            DiscountAmount: discountAmount,
            SurchargeAmount: surchargeAmount,
            TotalAmount: totalAmount,
            DepositAmount: model.DepositAmount);
    }

    public decimal CalculateLateFee(EquipmentModel model, DateOnly dueDate, DateOnly actualReturnDate)
    {
        var daysLate = actualReturnDate.DayNumber - dueDate.DayNumber;
        if (daysLate <= 0)
        {
            return 0m;
        }

        return Math.Round(model.DailyRate * LateFeeMultiplier * daysLate, 2);
    }

    public decimal CalculateDamageFee(EquipmentModel model, decimal depositAmount, UnitCondition reportedCondition)
    {
        var fraction = reportedCondition switch
        {
            UnitCondition.Good => 0m,
            UnitCondition.Fair => 0.25m,
            UnitCondition.Damaged => 0.75m,
            UnitCondition.Retired => 1.0m,
            _ => 0m
        };

        return Math.Round(Math.Min(depositAmount, depositAmount * fraction), 2);
    }

    private static int CountWeekendDays(DateOnly startDate, DateOnly endDate)
    {
        var count = 0;
        for (var day = startDate; day < endDate; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }
}
