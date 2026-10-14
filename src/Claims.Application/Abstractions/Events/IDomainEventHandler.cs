using Claims.Domain.Abstractions;

namespace Claims.Application.Abstractions.Events;

public sealed record EventMetadata(Guid EventId, DateTimeOffset OccurredOnUtc);

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken);
}
