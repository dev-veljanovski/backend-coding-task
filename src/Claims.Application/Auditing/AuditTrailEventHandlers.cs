using Claims.Application.Abstractions.Auditing;
using Claims.Application.Abstractions.Events;
using Claims.Domain.Claims.Events;
using Claims.Domain.Covers.Events;

namespace Claims.Application.Auditing;

// Task 3: writes the audit rows (same tables as before) from domain events, off the request path.
internal sealed class AuditTrailEventHandlers(IAuditTrail auditTrail) :
    IDomainEventHandler<ClaimCreatedDomainEvent>,
    IDomainEventHandler<ClaimDeletedDomainEvent>,
    IDomainEventHandler<CoverCreatedDomainEvent>,
    IDomainEventHandler<CoverDeletedDomainEvent>
{
    public Task Handle(ClaimCreatedDomainEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken) =>
        Record(metadata, AuditedEntity.Claim, domainEvent.ClaimId, AuditAction.Created, cancellationToken);

    public Task Handle(ClaimDeletedDomainEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken) =>
        Record(metadata, AuditedEntity.Claim, domainEvent.ClaimId, AuditAction.Deleted, cancellationToken);

    public Task Handle(CoverCreatedDomainEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken) =>
        Record(metadata, AuditedEntity.Cover, domainEvent.CoverId, AuditAction.Created, cancellationToken);

    public Task Handle(CoverDeletedDomainEvent domainEvent, EventMetadata metadata, CancellationToken cancellationToken) =>
        Record(metadata, AuditedEntity.Cover, domainEvent.CoverId, AuditAction.Deleted, cancellationToken);

    private Task Record(
        EventMetadata metadata,
        AuditedEntity entity,
        Guid entityId,
        AuditAction action,
        CancellationToken cancellationToken) =>
        auditTrail.RecordAsync(
            new AuditRecord(metadata.EventId, entity, entityId, action, metadata.OccurredOnUtc),
            cancellationToken);
}
