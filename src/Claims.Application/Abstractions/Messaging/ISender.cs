using Claims.Domain.Results;

namespace Claims.Application.Abstractions.Messaging;

public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IOperationResult<TResponse>;
}
