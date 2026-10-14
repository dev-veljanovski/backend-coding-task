using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Claims;
using Claims.Domain.Covers;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Claims.CreateClaim;

public sealed record CreateClaimCommand(
    Guid CoverId,
    string Name,
    ClaimType Type,
    decimal DamageCost,
    DateOnly Created) : ICommand<ClaimResponse>;

internal sealed class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public const int NameMaxLength = 200;

    public CreateClaimCommandValidator()
    {
        RuleFor(c => c.CoverId)
            .NotEmpty().WithErrorCode("Claim.CoverIdRequired");

        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("Claim.NameRequired")
            .MaximumLength(NameMaxLength).WithErrorCode("Claim.NameTooLong");

        RuleFor(c => c.Type)
            .IsInEnum().WithErrorCode("Claim.TypeInvalid");

        RuleFor(c => c.Created)
            .NotEmpty().WithErrorCode("Claim.CreatedRequired");
    }
}

internal sealed class CreateClaimCommandHandler(ICoverRepository covers, IClaimRepository claims)
    : ICommandHandler<CreateClaimCommand, ClaimResponse>
{
    public async Task<Result<ClaimResponse>> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var coverId = new CoverId(request.CoverId);
        var cover = await covers.GetByIdAsync(coverId, cancellationToken).ConfigureAwait(false);
        if (cover is null)
        {
            return ClaimErrors.CoverDoesNotExist(coverId);
        }

        var claim = Claim.File(cover, request.Name, request.Type, request.DamageCost, request.Created);
        if (claim.IsFailure)
        {
            return Result<ClaimResponse>.Failure(claim.Errors);
        }

        claims.Add(claim.Value);
        return ClaimResponse.FromDomain(claim.Value);
    }
}
