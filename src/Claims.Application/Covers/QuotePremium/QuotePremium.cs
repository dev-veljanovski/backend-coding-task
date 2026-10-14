using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Covers;
using Claims.Domain.Covers.Pricing;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Covers.QuotePremium;

public sealed record QuotePremiumQuery(DateOnly StartDate, DateOnly EndDate, CoverType Type) : IQuery<PremiumQuoteResponse>;

public sealed record PremiumQuoteResponse(CoverType Type, DateOnly StartDate, DateOnly EndDate, int InsuredDays, decimal Premium);

internal sealed class QuotePremiumQueryValidator : AbstractValidator<QuotePremiumQuery>
{
    public QuotePremiumQueryValidator()
    {
        RuleFor(q => q.StartDate).NotEmpty().WithErrorCode("Cover.StartDateRequired");
        RuleFor(q => q.EndDate).NotEmpty().WithErrorCode("Cover.EndDateRequired");
        RuleFor(q => q.Type).IsInEnum().WithErrorCode("Cover.TypeInvalid");
    }
}

internal sealed class QuotePremiumQueryHandler(IPremiumCalculator premiumCalculator)
    : IQueryHandler<QuotePremiumQuery, PremiumQuoteResponse>
{
    public Task<Result<PremiumQuoteResponse>> Handle(QuotePremiumQuery request, CancellationToken cancellationToken)
    {
        var period = InsurancePeriod.Create(request.StartDate, request.EndDate);

        var result = period.Map(p => new PremiumQuoteResponse(
            request.Type,
            p.StartDate,
            p.EndDate,
            p.DurationInDays,
            premiumCalculator.Calculate(request.Type, p)));

        return Task.FromResult(result);
    }
}
