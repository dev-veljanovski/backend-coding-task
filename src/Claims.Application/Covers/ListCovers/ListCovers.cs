using Claims.Application.Abstractions.Messaging;
using Claims.Application.Common;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Covers.ListCovers;

public sealed record ListCoversQuery(int Page = 1, int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<CoverResponse>>;

internal sealed class ListCoversQueryValidator : AbstractValidator<ListCoversQuery>
{
    public ListCoversQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithErrorCode("Paging.PageInvalid");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize).WithErrorCode("Paging.PageSizeInvalid");
    }
}

internal sealed class ListCoversQueryHandler(ICoverReadService readService)
    : IQueryHandler<ListCoversQuery, PagedResult<CoverResponse>>
{
    public async Task<Result<PagedResult<CoverResponse>>> Handle(ListCoversQuery request, CancellationToken cancellationToken) =>
        await readService.ListAsync(request.Page, request.PageSize, cancellationToken).ConfigureAwait(false);
}
