using Claims.Domain.Covers;
using Claims.Domain.Covers.Pricing;

namespace Claims.UnitTests.TestDoubles;

internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 10, 14);

    public static readonly IPremiumCalculator PremiumCalculator = new PremiumCalculator(new StandardPremiumTariff());

    public static Cover NewCover(DateOnly? startDate = null, DateOnly? endDate = null, CoverType type = CoverType.Yacht)
    {
        var start = startDate ?? Today;
        var end = endDate ?? start.AddMonths(6);
        return Cover.Issue(start, end, type, Today, PremiumCalculator).Value;
    }

    public static InsurancePeriod Period(int days, DateOnly? startDate = null)
    {
        var start = startDate ?? Today;
        return InsurancePeriod.Create(start, start.AddDays(days)).Value;
    }
}
