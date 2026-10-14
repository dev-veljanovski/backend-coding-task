using Claims.Application.Common;
using Claims.Domain.Claims;

namespace Claims.Application.Claims;

public sealed record ClaimResponse(
    Guid Id,
    Guid CoverId,
    string Name,
    ClaimType Type,
    decimal DamageCost,
    DateOnly Created)
{
    public static ClaimResponse FromDomain(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return new ClaimResponse(
            claim.Id.Value,
            claim.CoverId.Value,
            claim.Name,
            claim.Type,
            claim.DamageCost.Amount,
            claim.Created);
    }
}

public interface IClaimReadService
{
    Task<ClaimResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ClaimResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
