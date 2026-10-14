using Claims.Application.Abstractions.Messaging;
using Claims.Application.Common;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Claims.ListClaims;

public sealed record ListClaimsQuery(int Page = 1, int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<ClaimResponse>>;

internal sealed class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithErrorCode("Paging.PageInvalid");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize).WithErrorCode("Paging.PageSizeInvalid");
    }
}

internal sealed class ListClaimsQueryHandler(IClaimReadService readService)
    : IQueryHandler<ListClaimsQuery, PagedResult<ClaimResponse>>
{
    public async Task<Result<PagedResult<ClaimResponse>>> Handle(ListClaimsQuery request, CancellationToken cancellationToken) =>
        await readService.ListAsync(request.Page, request.PageSize, cancellationToken).ConfigureAwait(false);
}
