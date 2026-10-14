using System.Diagnostics;
using Claims.Application.Abstractions.Auditing;
using Claims.Application.Abstractions.Events;
using Claims.Application.Auditing;
using Claims.Domain.Abstractions;
using Claims.Domain.Claims.Events;
using Claims.Domain.Covers.Events;
using Claims.Infrastructure.Outbox;
using NSubstitute;

namespace Claims.UnitTests.Infrastructure;

public sealed class DomainEventSerializerTests
{
    private readonly DomainEventSerializer _serializer = new();

    public static TheoryData<IDomainEvent> AllDomainEvents() =>
    [
        new ClaimCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid()),
        new ClaimDeletedDomainEvent(Guid.NewGuid()),
        new CoverCreatedDomainEvent(Guid.NewGuid()),
        new CoverDeletedDomainEvent(Guid.NewGuid()),
    ];

    [Theory]
    [MemberData(nameof(AllDomainEvents))]
    public void Serialize_ThenDeserialize_RoundTripsEveryDomainEvent(IDomainEvent domainEvent)
    {
        var (type, payload) = _serializer.Serialize(domainEvent);

        _serializer.Deserialize(type, payload).ShouldBe(domainEvent);
    }

    [Fact]
    public void Serialize_UsesStableTypeNameRatherThanClrName()
    {
        var (type, _) = _serializer.Serialize(new ClaimDeletedDomainEvent(Guid.NewGuid()));

        type.ShouldBe(nameof(ClaimDeletedDomainEvent));
    }

    [Fact]
    public void Deserialize_UnknownType_Throws()
    {
        Should.Throw<InvalidOperationException>(() => _serializer.Deserialize("NoSuchEvent", "{}"));
    }
}

public sealed class OutboxSignalTests
{
    [Fact]
    public async Task WaitAsync_ReturnsImmediately_WhenNotified()
    {
        var signal = new OutboxSignal();
        signal.Notify();

        var stopwatch = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WaitAsync_ReturnsAfterTimeout_WhenNotNotified()
    {
        var signal = new OutboxSignal();

        var stopwatch = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90));
    }

    [Fact]
    public async Task Notify_ManyTimes_CollapsesIntoASingleWakeUp()
    {
        var signal = new OutboxSignal();
        for (var i = 0; i < 100; i++)
        {
            signal.Notify();
        }

        await signal.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var stopwatch = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90));
    }

    [Fact]
    public async Task WaitAsync_HonoursCancellation()
    {
        var signal = new OutboxSignal();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(() => signal.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token));
    }
}

public sealed class AuditTrailEventHandlersTests
{
    private readonly IAuditTrail _auditTrail = Substitute.For<IAuditTrail>();
    private readonly EventMetadata _metadata = new(Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task ClaimCreated_IsRecordedAsCreatedClaim()
    {
        var claimId = Guid.NewGuid();

        await new AuditTrailEventHandlers(_auditTrail)
            .Handle(new ClaimCreatedDomainEvent(claimId, Guid.NewGuid()), _metadata, TestContext.Current.CancellationToken);

        await _auditTrail.Received(1).RecordAsync(
            new AuditRecord(_metadata.EventId, AuditedEntity.Claim, claimId, AuditAction.Created, _metadata.OccurredOnUtc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CoverDeleted_IsRecordedAsDeletedCover()
    {
        var coverId = Guid.NewGuid();

        await new AuditTrailEventHandlers(_auditTrail)
            .Handle(new CoverDeletedDomainEvent(coverId), _metadata, TestContext.Current.CancellationToken);

        await _auditTrail.Received(1).RecordAsync(
            new AuditRecord(_metadata.EventId, AuditedEntity.Cover, coverId, AuditAction.Deleted, _metadata.OccurredOnUtc),
            Arg.Any<CancellationToken>());
    }
}
