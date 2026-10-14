using Claims.Application.Abstractions.Auditing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Auditing;

// Delivery is at-least-once, so the same event can arrive twice - the unique EventId makes the insert idempotent.
internal sealed partial class SqlAuditTrail(AuditDbContext db, ILogger<SqlAuditTrail> logger) : IAuditTrail
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (await IsAlreadyRecordedAsync(record, cancellationToken).ConfigureAwait(false))
        {
            LogDuplicate(logger, record.EventId);
            return;
        }

        var created = record.OccurredOnUtc.UtcDateTime;
        var httpRequestType = record.Action switch
        {
            AuditAction.Created => "POST",
            AuditAction.Deleted => "DELETE",
            _ => throw new ArgumentOutOfRangeException(nameof(record), record.Action, "Unknown audit action."),
        };

        switch (record.Entity)
        {
            case AuditedEntity.Claim:
                db.ClaimAudits.Add(new ClaimAudit
                {
                    ClaimId = record.EntityId.ToString(),
                    Created = created,
                    HttpRequestType = httpRequestType,
                    EventId = record.EventId,
                });
                break;

            case AuditedEntity.Cover:
                db.CoverAudits.Add(new CoverAudit
                {
                    CoverId = record.EntityId.ToString(),
                    Created = created,
                    HttpRequestType = httpRequestType,
                    EventId = record.EventId,
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(record), record.Entity, "Unknown audited entity.");
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            db.ChangeTracker.Clear();
            LogDuplicate(logger, record.EventId);
        }
    }

    private Task<bool> IsAlreadyRecordedAsync(AuditRecord record, CancellationToken cancellationToken) =>
        record.Entity == AuditedEntity.Claim
            ? db.ClaimAudits.AnyAsync(a => a.EventId == record.EventId, cancellationToken)
            : db.CoverAudits.AnyAsync(a => a.EventId == record.EventId, cancellationToken);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Audit record for event {EventId} already exists; skipped")]
    private static partial void LogDuplicate(ILogger logger, Guid eventId);
}

public sealed class AuditDatabaseOptions
{
    public const string SectionName = "AuditDatabase";

    public bool ApplyMigrationsOnStartup { get; set; } = true;
}

// The database is created in its own retried step before migrating. Letting Migrate create it under the
// retrying execution strategy can fail with "database already exists" while SQL Server is still starting.
internal sealed partial class AuditDatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditDatabaseOptions> options,
    ILogger<AuditDatabaseInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 10;
    private const int DatabaseAlreadyExists = 1801;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        await EnsureDatabaseExistsAsync(db, cancellationToken).ConfigureAwait(false);
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        LogMigrated(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureDatabaseExistsAsync(AuditDbContext db, CancellationToken cancellationToken)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!await creator.ExistsAsync(cancellationToken).ConfigureAwait(false))
                {
                    await creator.CreateAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }
            catch (SqlException exception) when (exception.Number == DatabaseAlreadyExists)
            {
                return;
            }
            catch (SqlException exception) when (attempt < MaxAttempts)
            {
                var delay = BaseDelay * attempt;
                LogDatabaseNotReady(logger, exception, attempt, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit database is up to date")]
    private static partial void LogMigrated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit database server not ready (attempt {Attempt}); retrying in {DelaySeconds} s")]
    private static partial void LogDatabaseNotReady(ILogger logger, Exception exception, int attempt, double delaySeconds);
}
