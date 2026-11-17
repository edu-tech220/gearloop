using GearLoop.Api.Domain;
using GearLoop.Api.Services;
using Xunit;

namespace GearLoop.Api.Tests;

public class PricingServiceTests
{
    private readonly PricingService _sut = new();

    private static EquipmentModel BuildModel(decimal dailyRate = 50m, decimal deposit = 200m) => new()
    {
        Name = "Test Model",
        Category = "Test",
        Description = "Test",
        DailyRate = dailyRate,
        DepositAmount = deposit,
        ReplacementValue = 1000m
    };

    private static DateOnly NextMonday()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    [Fact]
    public void CalculateRentalPrice_ShortWeekdayRental_HasNoDiscountOrSurcharge()
    {
        var monday = NextMonday();
        var price = _sut.CalculateRentalPrice(BuildModel(dailyRate: 50m), monday, monday.AddDays(2));

        Assert.Equal(2, price.RentalDays);
        Assert.Equal(100m, price.BaseAmount);
        Assert.Equal(0m, price.DiscountAmount);
        Assert.Equal(0m, price.SurchargeAmount);
        Assert.Equal(100m, price.TotalAmount);
    }

    [Fact]
    public void CalculateRentalPrice_ThreeToSixDays_Gets10PercentDiscount()
    {
        var monday = NextMonday();
        var price = _sut.CalculateRentalPrice(BuildModel(dailyRate: 50m), monday, monday.AddDays(3));

        Assert.Equal(150m, price.BaseAmount);
        Assert.Equal(15m, price.DiscountAmount);
    }

    [Fact]
    public void CalculateRentalPrice_SevenOrMoreDays_Gets15PercentDiscount()
    {
        var monday = NextMonday();
        var price = _sut.CalculateRentalPrice(BuildModel(dailyRate: 50m), monday, monday.AddDays(7));

        Assert.Equal(350m, price.BaseAmount);
        Assert.Equal(52.5m, price.DiscountAmount);
    }

    [Fact]
    public void CalculateRentalPrice_RangeCoveringAWeekend_AddsSurchargePerWeekendDay()
    {
        var saturday = NextMonday().AddDays(5);
        var monday = saturday.AddDays(2);

        var price = _sut.CalculateRentalPrice(BuildModel(dailyRate: 50m), saturday, monday);

        Assert.Equal(10m, price.SurchargeAmount); // 2 weekend days (Sat & Sun) * 50 * 0.10
    }

    [Fact]
    public void CalculateLateFee_ReturnedOnOrBeforeDueDate_IsZero()
    {
        var dueDate = NextMonday();
        Assert.Equal(0m, _sut.CalculateLateFee(BuildModel(dailyRate: 40m), dueDate, dueDate));
        Assert.Equal(0m, _sut.CalculateLateFee(BuildModel(dailyRate: 40m), dueDate, dueDate.AddDays(-1)));
    }

    [Fact]
    public void CalculateLateFee_ReturnedLate_Charges150PercentPerDayLate()
    {
        var dueDate = NextMonday();
        var fee = _sut.CalculateLateFee(BuildModel(dailyRate: 40m), dueDate, dueDate.AddDays(2));

        Assert.Equal(120m, fee); // 40 * 1.5 * 2
    }

    [Theory]
    [InlineData(UnitCondition.Good, 0)]
    [InlineData(UnitCondition.Fair, 50)]
    [InlineData(UnitCondition.Damaged, 150)]
    [InlineData(UnitCondition.Retired, 200)]
    public void CalculateDamageFee_ReflectsConditionSeverity(UnitCondition condition, decimal expectedFee)
    {
        var fee = _sut.CalculateDamageFee(BuildModel(), depositAmount: 200m, condition);
        Assert.Equal(expectedFee, fee);
    }

    [Fact]
    public void CalculateDamageFee_NeverExceedsTheDeposit()
    {
        var fee = _sut.CalculateDamageFee(BuildModel(), depositAmount: 10m, UnitCondition.Retired);
        Assert.Equal(10m, fee);
    }
}
