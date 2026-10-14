using Claims.Domain.Results;

namespace Claims.Domain.Claims;

// Task 2: a claim can be at most 100.000.
public readonly record struct DamageCost
{
    public const decimal MaxAmount = 100_000m;

    private DamageCost(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Result<DamageCost> Create(decimal amount)
    {
        if (amount <= 0m)
        {
            return ClaimErrors.DamageCostNotPositive;
        }

        if (amount > MaxAmount)
        {
            return ClaimErrors.DamageCostExceedsLimit;
        }

        return new DamageCost(amount);
    }

    public static DamageCost Rehydrate(decimal amount) => new(amount);

    public override string ToString() => Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
}
