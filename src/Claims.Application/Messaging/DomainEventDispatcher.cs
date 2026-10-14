using System.Collections.Concurrent;
using Claims.Application.Abstractions.Events;
using Claims.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Application.Messaging;

internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, DomainEventHandlerWrapper> Wrappers = new();

    public Task DispatchAsync(IDomainEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        var wrapper = Wrappers.GetOrAdd(
            domainEvent.GetType(),
            static eventType => (DomainEventHandlerWrapper)Activator.CreateInstance(
                typeof(DomainEventHandlerWrapper<>).MakeGenericType(eventType))!);

        return wrapper.Handle(domainEvent, metadata, serviceProvider, cancellationToken);
    }
}

internal abstract class DomainEventHandlerWrapper
{
    public abstract Task Handle(IDomainEvent domainEvent, EventMetadata metadata, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class DomainEventHandlerWrapper<TEvent> : DomainEventHandlerWrapper
    where TEvent : IDomainEvent
{
    public override async Task Handle(IDomainEvent domainEvent, EventMetadata metadata, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.Handle((TEvent)domainEvent, metadata, cancellationToken).ConfigureAwait(false);
        }
    }
}
