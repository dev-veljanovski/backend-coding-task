using Claims.Domain.Abstractions;

namespace Claims.Domain.Claims.Events;

public sealed record ClaimCreatedDomainEvent(Guid ClaimId, Guid CoverId) : IDomainEvent;

public sealed record ClaimDeletedDomainEvent(Guid ClaimId) : IDomainEvent;
