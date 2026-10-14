using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Claims;
using Claims.Domain.Results;

namespace Claims.Application.Claims.GetClaim;

public sealed record GetClaimQuery(Guid ClaimId) : IQuery<ClaimResponse>;

internal sealed class GetClaimQueryHandler(IClaimReadService readService) : IQueryHandler<GetClaimQuery, ClaimResponse>
{
    public async Task<Result<ClaimResponse>> Handle(GetClaimQuery request, CancellationToken cancellationToken)
    {
        var claim = await readService.GetByIdAsync(request.ClaimId, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return ClaimErrors.NotFound(new ClaimId(request.ClaimId));
        }

        return claim;
    }
}
