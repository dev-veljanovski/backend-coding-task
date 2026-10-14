using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Covers;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Covers.DeleteCover;

public sealed record DeleteCoverCommand(Guid CoverId) : ICommand;

internal sealed class DeleteCoverCommandValidator : AbstractValidator<DeleteCoverCommand>
{
    public DeleteCoverCommandValidator() =>
        RuleFor(c => c.CoverId).NotEmpty().WithErrorCode("Cover.IdRequired");
}

internal sealed class DeleteCoverCommandHandler(ICoverRepository covers) : ICommandHandler<DeleteCoverCommand>
{
    public async Task<Result> Handle(DeleteCoverCommand request, CancellationToken cancellationToken)
    {
        var coverId = new CoverId(request.CoverId);
        var cover = await covers.GetByIdAsync(coverId, cancellationToken).ConfigureAwait(false);
        if (cover is null)
        {
            return CoverErrors.NotFound(coverId);
        }

        cover.Delete();
        covers.Remove(cover);
        return Result.Success();
    }
}
