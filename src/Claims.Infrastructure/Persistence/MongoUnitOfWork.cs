using System.Diagnostics;
using Claims.Application.Abstractions.Persistence;
using Claims.Domain.Abstractions;
using Claims.Infrastructure.Outbox;
using MongoDB.Driver;

namespace Claims.Infrastructure.Persistence;

// Task 3: the change and its domain events (outbox) are saved in one Mongo transaction.
// The HTTP request ends here - the audit DB is written later by OutboxProcessor.
internal sealed class MongoUnitOfWork(
    MongoDbContext context,
    DomainEventSerializer serializer,
    IOutboxSignal outboxSignal,
    TimeProvider timeProvider) : IUnitOfWork
{
    private readonly List<Func<IClientSessionHandle, CancellationToken, Task>> _operations = [];
    private readonly HashSet<IAggregateRoot> _aggregates = new(ReferenceEqualityComparer.Instance);

    public void Register(IAggregateRoot aggregate, Func<IClientSessionHandle, CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(operation);

        _aggregates.Add(aggregate);
        _operations.Add(operation);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_operations.Count == 0)
        {
            return;
        }

        var outboxMessages = CreateOutboxMessages();

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                foreach (var operation in _operations)
                {
                    await operation(transactionSession, token).ConfigureAwait(false);
                }

                if (outboxMessages.Count > 0)
                {
                    await context.OutboxMessages
                        .InsertManyAsync(transactionSession, outboxMessages, cancellationToken: token)
                        .ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var aggregate in _aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        _operations.Clear();
        _aggregates.Clear();

        if (outboxMessages.Count > 0)
        {
            outboxSignal.Notify();
        }
    }

    private List<OutboxMessage> CreateOutboxMessages()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var traceParent = Activity.Current?.Id;

        return _aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .Select(domainEvent =>
            {
                var (type, payload) = serializer.Serialize(domainEvent);
                return new OutboxMessage
                {
                    Id = Guid.CreateVersion7().ToString(),
                    Type = type,
                    Payload = payload,
                    OccurredOnUtc = now,
                    TraceParent = traceParent,
                    NextAttemptOnUtc = now,
                };
            })
            .ToList();
    }
}
