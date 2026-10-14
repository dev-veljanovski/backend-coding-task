using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Claims;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Claims.DeleteClaim;

public sealed record DeleteClaimCommand(Guid ClaimId) : ICommand;

internal sealed class DeleteClaimCommandValidator : AbstractValidator<DeleteClaimCommand>
{
    public DeleteClaimCommandValidator() =>
        RuleFor(c => c.ClaimId).NotEmpty().WithErrorCode("Claim.IdRequired");
}

internal sealed class DeleteClaimCommandHandler(IClaimRepository claims) : ICommandHandler<DeleteClaimCommand>
{
    public async Task<Result> Handle(DeleteClaimCommand request, CancellationToken cancellationToken)
    {
        var claimId = new ClaimId(request.ClaimId);
        var claim = await claims.GetByIdAsync(claimId, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return ClaimErrors.NotFound(claimId);
        }

        claim.Delete();
        claims.Remove(claim);
        return Result.Success();
    }
}
