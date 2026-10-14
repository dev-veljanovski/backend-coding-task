using Claims.Domain.Results;

namespace Claims.Domain.Covers;

public static class CoverErrors
{
    public static Error NotFound(CoverId id) =>
        Error.NotFound("Cover.NotFound", $"Cover '{id}' was not found.");

    public static readonly Error EndDateNotAfterStartDate = Error.Validation(
        "Cover.EndDateNotAfterStartDate",
        "The end date must be after the start date.",
        "endDate");

    public static readonly Error PeriodExceedsOneYear = Error.Validation(
        "Cover.PeriodExceedsOneYear",
        $"The total insurance period cannot exceed {InsurancePeriod.MaxLengthInYears} year.",
        "endDate");

    public static Error StartDateInPast(DateOnly today) => Error.Validation(
        "Cover.StartDateInPast",
        $"The start date cannot be in the past (today is {today:yyyy-MM-dd}).",
        "startDate");
}
