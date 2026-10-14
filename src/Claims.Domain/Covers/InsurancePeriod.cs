using Claims.Domain.Results;

namespace Claims.Domain.Covers;

public sealed record InsurancePeriod
{
    public const int MaxLengthInYears = 1;

    private InsurancePeriod(DateOnly startDate, DateOnly endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    // Same day count as the original code: EndDate - StartDate.
    public int DurationInDays => EndDate.DayNumber - StartDate.DayNumber;

    // Task 2: the total insurance period can't exceed one year (leap years included).
    public static Result<InsurancePeriod> Create(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            return CoverErrors.EndDateNotAfterStartDate;
        }

        if (endDate > startDate.AddYears(MaxLengthInYears))
        {
            return CoverErrors.PeriodExceedsOneYear;
        }

        return new InsurancePeriod(startDate, endDate);
    }

    public static InsurancePeriod Rehydrate(DateOnly startDate, DateOnly endDate) => new(startDate, endDate);

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}
