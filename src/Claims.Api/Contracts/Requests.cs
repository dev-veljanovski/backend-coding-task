using System.ComponentModel;
using Claims.Domain.Claims;
using Claims.Domain.Covers;

namespace Claims.Api.Contracts;

public sealed record CreateClaimRequest(
    [property: Description("Id of an existing cover.")] Guid CoverId,
    [property: Description("Short description of the claim (max 200 characters).")] string Name,
    [property: Description("The cause of the damage.")] ClaimType Type,
    [property: Description("The claimed amount. Must be greater than 0 and cannot exceed 100.000.")] decimal DamageCost,
    [property: Description("Date of the claim (yyyy-MM-dd). Must be within the period of the cover.")] DateOnly Created);

public sealed record CreateCoverRequest(
    [property: Description("First insured day (yyyy-MM-dd). Cannot be in the past.")] DateOnly StartDate,
    [property: Description("Last insured day (yyyy-MM-dd). At most 1 year after the start date.")] DateOnly EndDate,
    [property: Description("The insured vessel type.")] CoverType Type);
