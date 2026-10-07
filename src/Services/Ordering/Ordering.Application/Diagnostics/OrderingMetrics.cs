using System.Diagnostics.Metrics;

namespace Ordering.Application.Diagnostics;

/// <summary>Business metrics: exported to Prometheus and charted next to the technical ones in Grafana.</summary>
internal static class OrderingMetrics
{
    public const string MeterName = "Ordering.Application";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> OrdersPlaced = Meter.CreateCounter<long>("ordering.orders.placed", description: "Orders placed.");

    public static readonly Counter<long> OrdersConfirmed = Meter.CreateCounter<long>("ordering.orders.confirmed", description: "Orders confirmed after stock reservation.");

    public static readonly Counter<long> OrdersRejected = Meter.CreateCounter<long>("ordering.orders.rejected", description: "Orders rejected because stock could not be reserved.");
}
