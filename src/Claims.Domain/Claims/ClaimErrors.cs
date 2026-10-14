using System.Globalization;
using Claims.Domain.Covers;
using Claims.Domain.Results;

namespace Claims.Domain.Claims;

public static class ClaimErrors
{
    public static Error NotFound(ClaimId id) =>
        Error.NotFound("Claim.NotFound", $"Claim '{id}' was not found.");

    public static Error CoverDoesNotExist(CoverId coverId) => Error.Validation(
        "Claim.CoverDoesNotExist",
        $"Cover '{coverId}' does not exist.",
        "coverId");

    public static readonly Error NameRequired = Error.Validation(
        "Claim.NameRequired",
        "The claim name is required.",
        "name");

    public static readonly Error DamageCostNotPositive = Error.Validation(
        "Claim.DamageCostNotPositive",
        "The damage cost must be greater than zero.",
        "damageCost");

    public static readonly Error DamageCostExceedsLimit = Error.Validation(
        "Claim.DamageCostExceedsLimit",
        string.Create(CultureInfo.InvariantCulture, $"The damage cost cannot exceed {DamageCost.MaxAmount:N0}."),
        "damageCost");

    public static Error CreatedOutsideCoverPeriod(InsurancePeriod period) => Error.Validation(
        "Claim.CreatedOutsideCoverPeriod",
        $"The created date must be within the period of the related cover ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}).",
        "created");
}
