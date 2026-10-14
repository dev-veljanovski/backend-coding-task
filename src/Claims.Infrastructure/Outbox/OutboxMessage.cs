using MongoDB.Bson.Serialization.Attributes;

namespace Claims.Infrastructure.Outbox;

internal sealed class OutboxMessage
{
    [BsonId]
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public required DateTime OccurredOnUtc { get; init; }

    public string? TraceParent { get; init; }

    public DateTime NextAttemptOnUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public DateTime? DeadLetteredOnUtc { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public string? LockedBy { get; set; }
}
