using Claims.Infrastructure.Auditing;
using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Claims.Infrastructure.HealthChecks;

internal sealed class MongoHealthCheck(MongoDbContext mongo) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await mongo.Database
                .RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unreachable.", exception);
        }
    }
}

internal sealed class AuditDatabaseHealthCheck(AuditDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The audit database is unreachable.");
}

internal sealed class OutboxHealthCheck(MongoDbContext mongo, TimeProvider timeProvider) : IHealthCheck
{
    private static readonly TimeSpan MaxAcceptableLag = TimeSpan.FromMinutes(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OutboxMessage>.Filter;
        var threshold = timeProvider.GetUtcNow().UtcDateTime - MaxAcceptableLag;

        var deadLettered = await mongo.OutboxMessages
            .CountDocumentsAsync(filter.Ne(m => m.DeadLetteredOnUtc, null), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var stale = await mongo.OutboxMessages
            .CountDocumentsAsync(
                filter.And(
                    filter.Eq(m => m.ProcessedOnUtc, null),
                    filter.Eq(m => m.DeadLetteredOnUtc, null),
                    filter.Lt(m => m.OccurredOnUtc, threshold)),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var data = new Dictionary<string, object> { ["deadLettered"] = deadLettered, ["stale"] = stale };

        return deadLettered > 0 || stale > 0
            ? HealthCheckResult.Degraded("The audit outbox has stale or dead-lettered messages.", data: data)
            : HealthCheckResult.Healthy(data: data);
    }
}
