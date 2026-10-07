using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlocks.Application.Diagnostics;

public static class ApplicationDiagnostics
{
    public const string Name = "BuildingBlocks.Application";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "app.request.duration",
        unit: "ms",
        description: "Duration of command and query handling, including pipeline behaviors.");
}
