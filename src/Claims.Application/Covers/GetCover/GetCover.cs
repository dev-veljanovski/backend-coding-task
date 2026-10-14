using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Covers;
using Claims.Domain.Results;

namespace Claims.Application.Covers.GetCover;

public sealed record GetCoverQuery(Guid CoverId) : IQuery<CoverResponse>;

internal sealed class GetCoverQueryHandler(ICoverReadService readService) : IQueryHandler<GetCoverQuery, CoverResponse>
{
    public async Task<Result<CoverResponse>> Handle(GetCoverQuery request, CancellationToken cancellationToken)
    {
        var cover = await readService.GetByIdAsync(request.CoverId, cancellationToken).ConfigureAwait(false);
        if (cover is null)
        {
            return CoverErrors.NotFound(new CoverId(request.CoverId));
        }

        return cover;
    }
}
