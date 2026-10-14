using System.Collections.Concurrent;
using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Application.Messaging;

// Small in-house mediator instead of MediatR (commercial licence since v13).
internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IOperationResult<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) => Activator.CreateInstance(
                typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType))!,
            typeof(TResponse));

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }
}

internal abstract class RequestHandlerWrapper<TResponse>
    where TResponse : IOperationResult<TResponse>
{
    public abstract Task<TResponse> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOperationResult<TResponse>
{
    public override Task<TResponse> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

        foreach (var behavior in behaviors.Reverse())
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
        }

        return pipeline();
    }
}
