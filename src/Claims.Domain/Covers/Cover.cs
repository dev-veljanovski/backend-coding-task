using Claims.Domain.Abstractions;
using Claims.Domain.Covers.Events;
using Claims.Domain.Covers.Pricing;
using Claims.Domain.Results;

namespace Claims.Domain.Covers;

public sealed class Cover : AggregateRoot<CoverId>
{
    private Cover(CoverId id, InsurancePeriod period, CoverType type, decimal premium)
        : base(id)
    {
        Period = period;
        Type = type;
        Premium = premium;
    }

    public InsurancePeriod Period { get; }

    public CoverType Type { get; }

    public decimal Premium { get; }

    // Task 2: start date can't be in the past, the 1-year limit is checked in InsurancePeriod.
    // "today" is passed in so the rule is easy to test.
    public static Result<Cover> Issue(
        DateOnly startDate,
        DateOnly endDate,
        CoverType type,
        DateOnly today,
        IPremiumCalculator premiumCalculator)
    {
        ArgumentNullException.ThrowIfNull(premiumCalculator);

        var errors = new List<Error>();

        if (startDate < today)
        {
            errors.Add(CoverErrors.StartDateInPast(today));
        }

        var period = InsurancePeriod.Create(startDate, endDate);
        if (period.IsFailure)
        {
            errors.AddRange(period.Errors);
        }

        if (errors.Count > 0)
        {
            return Result<Cover>.Failure(errors);
        }

        var premium = premiumCalculator.Calculate(type, period.Value);
        var cover = new Cover(CoverId.New(), period.Value, type, premium);
        cover.Raise(new CoverCreatedDomainEvent(cover.Id.Value));
        return cover;
    }

    public static Cover Rehydrate(CoverId id, InsurancePeriod period, CoverType type, decimal premium) =>
        new(id, period, type, premium);

    public bool IsActiveOn(DateOnly date) => Period.Contains(date);

    public void Delete() => Raise(new CoverDeletedDomainEvent(Id.Value));
}
