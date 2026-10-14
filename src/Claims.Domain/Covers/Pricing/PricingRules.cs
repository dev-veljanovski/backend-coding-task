namespace Claims.Domain.Covers.Pricing;

public sealed record DiscountTier(int FromDay, int? ToDay, decimal Discount)
{
    public int DaysCoveredOutOf(int totalDays)
    {
        var upperBound = Math.Min(totalDays, ToDay ?? int.MaxValue);
        return Math.Max(0, upperBound - FromDay);
    }
}

public sealed record PricingProfile(decimal RiskSurcharge, IReadOnlyList<DiscountTier> Tiers);

public interface IPremiumTariff
{
    decimal BaseDayRate { get; }

    PricingProfile ProfileFor(CoverType coverType);
}

// Task 5: numbers from the requirements. Base rate 1250; Yacht +10%, Passenger ship +20%, Tanker +50%, others +30%.
// Days 31-180: -5% Yacht / -2% others. From day 181 an additional -3% / -1%.
public sealed class StandardPremiumTariff : IPremiumTariff
{
    public const int FullPriceDays = 30;

    public const int FirstDiscountDays = 150;

    private const int SecondBandStart = FullPriceDays;
    private const int ThirdBandStart = FullPriceDays + FirstDiscountDays;

    private static readonly PricingProfile Yacht = new(
        RiskSurcharge: 0.10m,
        Tiers:
        [
            new DiscountTier(0, SecondBandStart, Discount: 0.00m),
            new DiscountTier(SecondBandStart, ThirdBandStart, Discount: 0.05m),
            new DiscountTier(ThirdBandStart, ToDay: null, Discount: 0.05m + 0.03m),
        ]);

    private static readonly IReadOnlyList<DiscountTier> StandardTiers =
    [
        new DiscountTier(0, SecondBandStart, Discount: 0.00m),
        new DiscountTier(SecondBandStart, ThirdBandStart, Discount: 0.02m),
        new DiscountTier(ThirdBandStart, ToDay: null, Discount: 0.02m + 0.01m),
    ];

    private static readonly PricingProfile PassengerShip = new(RiskSurcharge: 0.20m, StandardTiers);
    private static readonly PricingProfile Tanker = new(RiskSurcharge: 0.50m, StandardTiers);
    private static readonly PricingProfile OtherVessel = new(RiskSurcharge: 0.30m, StandardTiers);

    public decimal BaseDayRate => 1250m;

    public PricingProfile ProfileFor(CoverType coverType) => coverType switch
    {
        CoverType.Yacht => Yacht,
        CoverType.PassengerShip => PassengerShip,
        CoverType.Tanker => Tanker,
        _ => OtherVessel,
    };
}
