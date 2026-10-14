using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Covers;
using Claims.Domain.Covers.Pricing;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Covers.CreateCover;

public sealed record CreateCoverCommand(DateOnly StartDate, DateOnly EndDate, CoverType Type) : ICommand<CoverResponse>;

internal sealed class CreateCoverCommandValidator : AbstractValidator<CreateCoverCommand>
{
    public CreateCoverCommandValidator()
    {
        RuleFor(c => c.StartDate).NotEmpty().WithErrorCode("Cover.StartDateRequired");
        RuleFor(c => c.EndDate).NotEmpty().WithErrorCode("Cover.EndDateRequired");
        RuleFor(c => c.Type).IsInEnum().WithErrorCode("Cover.TypeInvalid");
    }
}

internal sealed class CreateCoverCommandHandler(
    ICoverRepository covers,
    IPremiumCalculator premiumCalculator,
    TimeProvider timeProvider) : ICommandHandler<CreateCoverCommand, CoverResponse>
{
    public Task<Result<CoverResponse>> Handle(CreateCoverCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var cover = Cover.Issue(request.StartDate, request.EndDate, request.Type, today, premiumCalculator);
        if (cover.IsFailure)
        {
            return Task.FromResult(Result<CoverResponse>.Failure(cover.Errors));
        }

        covers.Add(cover.Value);
        return Task.FromResult(Result<CoverResponse>.Success(CoverResponse.FromDomain(cover.Value)));
    }
}
