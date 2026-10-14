using Claims.Application.Abstractions.Messaging;
using Claims.Application.Abstractions.Persistence;
using Claims.Domain.Results;

namespace Claims.Application.Behaviors;

// Commits only after a successful command; queries and failed results never write anything.
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOperationResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IBaseCommand)
        {
            return await next().ConfigureAwait(false);
        }

        var response = await next().ConfigureAwait(false);

        if (response.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
