using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Claims.Infrastructure.Persistence;

internal sealed class MongoIndexInitializer(MongoDbContext context, IOptions<OutboxOptions> outboxOptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await context.Claims.Indexes.CreateOneAsync(
            new CreateIndexModel<ClaimDocument>(
                Builders<ClaimDocument>.IndexKeys.Ascending(c => c.CoverId),
                new CreateIndexOptions { Name = "ix_claims_coverId" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await context.OutboxMessages.Indexes.CreateOneAsync(
            new CreateIndexModel<OutboxMessage>(
                Builders<OutboxMessage>.IndexKeys
                    .Ascending(m => m.ProcessedOnUtc)
                    .Ascending(m => m.DeadLetteredOnUtc)
                    .Ascending(m => m.NextAttemptOnUtc)
                    .Ascending(m => m.OccurredOnUtc),
                new CreateIndexOptions { Name = "ix_outbox_pending" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await context.OutboxMessages.Indexes.CreateOneAsync(
            new CreateIndexModel<OutboxMessage>(
                Builders<OutboxMessage>.IndexKeys.Ascending(m => m.ProcessedOnUtc),
                new CreateIndexOptions
                {
                    Name = "ttl_outbox_processed",
                    ExpireAfter = outboxOptions.Value.ProcessedRetention,
                }),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
