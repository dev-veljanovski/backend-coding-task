using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Claims.Infrastructure.Diagnostics;

public static class InfrastructureDiagnostics
{
    public const string SourceName = "Claims.Infrastructure";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> OutboxMessagesPublished = Meter.CreateCounter<long>(
        "claims.outbox.messages.published", "{message}", "Outbox messages delivered successfully.");

    public static readonly Counter<long> OutboxMessagesFailed = Meter.CreateCounter<long>(
        "claims.outbox.messages.failed", "{message}", "Failed outbox delivery attempts.");

    public static readonly Counter<long> OutboxMessagesDeadLettered = Meter.CreateCounter<long>(
        "claims.outbox.messages.dead_lettered", "{message}", "Outbox messages abandoned after the maximum number of attempts.");

    public static readonly Histogram<double> OutboxDeliveryLag = Meter.CreateHistogram<double>(
        "claims.outbox.delivery.lag", "ms", "Time between committing a change and delivering its event.");
}
