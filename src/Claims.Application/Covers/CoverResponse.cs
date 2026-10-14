using Claims.Application.Common;
using Claims.Domain.Covers;

namespace Claims.Application.Covers;

public sealed record CoverResponse(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    CoverType Type,
    decimal Premium)
{
    public static CoverResponse FromDomain(Cover cover)
    {
        ArgumentNullException.ThrowIfNull(cover);
        return new CoverResponse(cover.Id.Value, cover.Period.StartDate, cover.Period.EndDate, cover.Type, cover.Premium);
    }
}

public interface ICoverReadService
{
    Task<CoverResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<CoverResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
