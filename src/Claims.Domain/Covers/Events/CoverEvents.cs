using Claims.Domain.Abstractions;

namespace Claims.Domain.Covers.Events;

public sealed record CoverCreatedDomainEvent(Guid CoverId) : IDomainEvent;

public sealed record CoverDeletedDomainEvent(Guid CoverId) : IDomainEvent;
