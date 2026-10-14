namespace Claims.Application.Abstractions.Auditing;

public enum AuditedEntity
{
    Claim = 0,

    Cover = 1,
}

public enum AuditAction
{
    Created = 0,

    Deleted = 1,
}

public sealed record AuditRecord(
    Guid EventId,
    AuditedEntity Entity,
    Guid EntityId,
    AuditAction Action,
    DateTimeOffset OccurredOnUtc);

public interface IAuditTrail
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken);
}
