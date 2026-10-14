namespace Claims.Domain.Covers.Pricing;

// Task 5: the old loop charged the first 30 days three times and days 31-180 twice, applied the
// last discount band to the wrong days and worked with fractional days. Now every band is priced
// once: days in band * daily rate * (1 - discount).
public sealed class PremiumCalculator(IPremiumTariff tariff) : IPremiumCalculator
{
    private const int MoneyDecimals = 2;

    public decimal Calculate(CoverType coverType, InsurancePeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        var profile = tariff.ProfileFor(coverType);
        var dailyRate = tariff.BaseDayRate * (1m + profile.RiskSurcharge);
        var insuredDays = period.DurationInDays;

        var premium = profile.Tiers.Sum(tier =>
            tier.DaysCoveredOutOf(insuredDays) * dailyRate * (1m - tier.Discount));

        return decimal.Round(premium, MoneyDecimals, MidpointRounding.AwayFromZero);
    }
}
