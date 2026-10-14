using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Claims.Application.Diagnostics;

public static class ApplicationDiagnostics
{
    public const string SourceName = "Claims.Application";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> RequestsCounter = Meter.CreateCounter<long>(
        "claims.application.requests",
        unit: "{request}",
        description: "Number of executed commands and queries.");

    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "claims.application.request.duration",
        unit: "ms",
        description: "Duration of commands and queries.");
}
