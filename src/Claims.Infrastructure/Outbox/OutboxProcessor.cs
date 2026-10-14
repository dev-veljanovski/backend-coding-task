using System.Diagnostics;
using Claims.Infrastructure.Diagnostics;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Claims.Infrastructure.Outbox;

// Task 3: background worker that delivers outbox messages to the audit handlers.
// Woken up right after a commit, retries with back-off, dead-letters after MaxAttempts.
internal sealed partial class OutboxProcessor(
    MongoDbContext context,
    IMessageBus messageBus,
    IOutboxSignal signal,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int MaxErrorLength = 2000;

    private readonly string _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger, _instanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delivered = await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);

                if (delivered < _options.BatchSize)
                {
                    await signal.WaitAsync(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogLoopFailed(logger, exception);
                await Task.Delay(_options.PollingInterval, timeProvider, stoppingToken)
                    .ContinueWith(static _ => { }, TaskScheduler.Default)
                    .ConfigureAwait(false);
            }
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var taken = 0;

        while (taken < _options.BatchSize)
        {
            var message = await TryLeaseNextAsync(cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                break;
            }

            taken++;
            await DeliverAsync(message, cancellationToken).ConfigureAwait(false);
        }

        return taken;
    }

    private Task<OutboxMessage?> TryLeaseNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var filter = Builders<OutboxMessage>.Filter;

        var due = filter.And(
            filter.Eq(m => m.ProcessedOnUtc, null),
            filter.Eq(m => m.DeadLetteredOnUtc, null),
            filter.Lte(m => m.NextAttemptOnUtc, now),
            filter.Or(
                filter.Eq(m => m.LockedUntilUtc, null),
                filter.Lt(m => m.LockedUntilUtc, now)));

        var lease = Builders<OutboxMessage>.Update
            .Set(m => m.LockedUntilUtc, now.Add(_options.LockDuration))
            .Set(m => m.LockedBy, _instanceId);

        return context.OutboxMessages.FindOneAndUpdateAsync<OutboxMessage?>(
            due,
            lease,
            new FindOneAndUpdateOptions<OutboxMessage, OutboxMessage?>
            {
                Sort = Builders<OutboxMessage>.Sort.Ascending(m => m.OccurredOnUtc),
                ReturnDocument = ReturnDocument.After,
            },
            cancellationToken);
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ActivityContext.TryParse(message.TraceParent, traceState: null, out var parentContext);
        using var activity = InfrastructureDiagnostics.ActivitySource.StartActivity(
            $"{message.Type} publish",
            ActivityKind.Producer,
            parentContext);
        activity?.SetTag("messaging.message.id", message.Id);

        var envelope = new MessageEnvelope(
            Guid.Parse(message.Id),
            message.Type,
            message.Payload,
            new DateTimeOffset(DateTime.SpecifyKind(message.OccurredOnUtc, DateTimeKind.Utc)),
            activity?.Id ?? message.TraceParent);

        try
        {
            await messageBus.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
            await MarkProcessedAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await MarkFailedAsync(message, exception, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MarkProcessedAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await context.OutboxMessages.UpdateOneAsync(
            m => m.Id == message.Id,
            Builders<OutboxMessage>.Update
                .Set(m => m.ProcessedOnUtc, now)
                .Set(m => m.LockedUntilUtc, null)
                .Set(m => m.LockedBy, null),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        InfrastructureDiagnostics.OutboxMessagesPublished.Add(1, new TagList { { "type", message.Type } });
        InfrastructureDiagnostics.OutboxDeliveryLag.Record((now - message.OccurredOnUtc).TotalMilliseconds);
        LogDelivered(logger, message.Type, message.Id);
    }

    private async Task MarkFailedAsync(OutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var attempts = message.Attempts + 1;
        var error = exception.ToString();
        if (error.Length > MaxErrorLength)
        {
            error = error[..MaxErrorLength];
        }

        var update = Builders<OutboxMessage>.Update
            .Set(m => m.Attempts, attempts)
            .Set(m => m.LastError, error)
            .Set(m => m.LockedUntilUtc, null)
            .Set(m => m.LockedBy, null);

        if (attempts >= _options.MaxAttempts)
        {
            update = update.Set(m => m.DeadLetteredOnUtc, now);
            InfrastructureDiagnostics.OutboxMessagesDeadLettered.Add(1, new TagList { { "type", message.Type } });
            LogDeadLettered(logger, exception, message.Type, message.Id, attempts);
        }
        else
        {
            var retryAt = now.Add(RetryDelay(attempts));
            update = update.Set(m => m.NextAttemptOnUtc, retryAt);
            InfrastructureDiagnostics.OutboxMessagesFailed.Add(1, new TagList { { "type", message.Type } });
            LogRetryScheduled(logger, exception, message.Type, message.Id, attempts, retryAt);
        }

        await context.OutboxMessages.UpdateOneAsync(
            m => m.Id == message.Id,
            update,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    internal TimeSpan RetryDelay(int attempts)
    {
        var seconds = Math.Pow(2, Math.Min(attempts, 20));
        var delay = TimeSpan.FromSeconds(seconds);
        return delay < _options.MaxRetryDelay ? delay : _options.MaxRetryDelay;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox processor {InstanceId} started")]
    private static partial void LogStarted(ILogger logger, string instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Delivered outbox message {MessageType} {MessageId}")]
    private static partial void LogDelivered(ILogger logger, string messageType, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery of {MessageType} {MessageId} failed (attempt {Attempts}); retrying at {RetryAt:O}")]
    private static partial void LogRetryScheduled(ILogger logger, Exception exception, string messageType, string messageId, int attempts, DateTime retryAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageType} {MessageId} dead-lettered after {Attempts} attempts")]
    private static partial void LogDeadLettered(ILogger logger, Exception exception, string messageType, string messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox processing loop failed; retrying after the polling interval")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);
}
