using Claims.Domain.Covers;
using Claims.Domain.Covers.Pricing;
using Claims.UnitTests.TestDoubles;

namespace Claims.UnitTests.Domain;

// Task 5: expected values were calculated by hand from the requirements.
public sealed class PremiumCalculatorTests
{
    private static readonly DateOnly LeapYearStart = new(2028, 1, 1);

    private readonly PremiumCalculator _calculator = new(new StandardPremiumTariff());

    [Theory]
    [InlineData(CoverType.Yacht, 1, "1375")]
    [InlineData(CoverType.Yacht, 30, "41250")]
    [InlineData(CoverType.Yacht, 31, "42556.25")]
    [InlineData(CoverType.Yacht, 180, "237187.50")]
    [InlineData(CoverType.Yacht, 181, "238452.50")]
    [InlineData(CoverType.Yacht, 365, "471212.50")]
    [InlineData(CoverType.Yacht, 366, "472477.50")]
    [InlineData(CoverType.PassengerShip, 1, "1500")]
    [InlineData(CoverType.PassengerShip, 30, "45000")]
    [InlineData(CoverType.PassengerShip, 31, "46470")]
    [InlineData(CoverType.PassengerShip, 180, "265500")]
    [InlineData(CoverType.PassengerShip, 181, "266955")]
    [InlineData(CoverType.PassengerShip, 365, "534675")]
    [InlineData(CoverType.Tanker, 1, "1875")]
    [InlineData(CoverType.Tanker, 30, "56250")]
    [InlineData(CoverType.Tanker, 31, "58087.50")]
    [InlineData(CoverType.Tanker, 180, "331875")]
    [InlineData(CoverType.Tanker, 181, "333693.75")]
    [InlineData(CoverType.Tanker, 365, "668343.75")]
    [InlineData(CoverType.ContainerShip, 1, "1625")]
    [InlineData(CoverType.ContainerShip, 30, "48750")]
    [InlineData(CoverType.ContainerShip, 31, "50342.50")]
    [InlineData(CoverType.ContainerShip, 180, "287625")]
    [InlineData(CoverType.ContainerShip, 181, "289201.25")]
    [InlineData(CoverType.ContainerShip, 365, "579231.25")]
    [InlineData(CoverType.BulkCarrier, 365, "579231.25")]
    public void Calculate_ReturnsExpectedPremium_ForEveryBandBoundary(CoverType type, int days, string expected)
    {
        var premium = _calculator.Calculate(type, TestData.Period(days, LeapYearStart));

        premium.ShouldBe(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(CoverType.Yacht)]
    [InlineData(CoverType.PassengerShip)]
    [InlineData(CoverType.ContainerShip)]
    [InlineData(CoverType.BulkCarrier)]
    [InlineData(CoverType.Tanker)]
    public void Calculate_MatchesDayByDayReferenceImplementation_ForEveryPossibleLength(CoverType type)
    {
        for (var days = 1; days <= 366; days++)
        {
            var expected = ReferencePremium(type, days);

            var actual = _calculator.Calculate(type, TestData.Period(days, LeapYearStart));

            actual.ShouldBe(expected, $"{type} for {days} days");
        }
    }

    [Fact]
    public void Calculate_IsMonotonic_LongerCoverNeverCostsLess()
    {
        var previous = 0m;
        for (var days = 1; days <= 365; days++)
        {
            var premium = _calculator.Calculate(CoverType.Tanker, TestData.Period(days));
            premium.ShouldBeGreaterThan(previous);
            previous = premium;
        }
    }

    [Fact]
    public void Calculate_YachtIsCheapestAndTankerMostExpensive_ForTheSamePeriod()
    {
        var period = TestData.Period(200);

        var yacht = _calculator.Calculate(CoverType.Yacht, period);
        var passenger = _calculator.Calculate(CoverType.PassengerShip, period);
        var container = _calculator.Calculate(CoverType.ContainerShip, period);
        var tanker = _calculator.Calculate(CoverType.Tanker, period);

        yacht.ShouldBeLessThan(passenger);
        passenger.ShouldBeLessThan(container);
        container.ShouldBeLessThan(tanker);
    }

    private static decimal ReferencePremium(CoverType type, int days)
    {
        var surcharge = type switch
        {
            CoverType.Yacht => 0.10m,
            CoverType.PassengerShip => 0.20m,
            CoverType.Tanker => 0.50m,
            _ => 0.30m,
        };
        var dayRate = 1250m * (1 + surcharge);
        var isYacht = type == CoverType.Yacht;

        var total = 0m;
        for (var day = 0; day < days; day++)
        {
            var discount = day switch
            {
                < 30 => 0m,
                < 180 => isYacht ? 0.05m : 0.02m,
                _ => isYacht ? 0.08m : 0.03m,
            };
            total += dayRate * (1 - discount);
        }

        return total;
    }
}
