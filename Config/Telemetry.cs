namespace AspNetCore10.OpenTelemetry.Study.Config;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class Telemetry
{
    public const string ServiceName = "AspNetCore10.OpenTelemetry.Study";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    public static readonly Counter<long> CustomersCreated =
        Meter.CreateCounter<long>(
            name: "customers.created",
            unit: "customers",
            description: "Total number of customers created.");

    public static readonly Counter<long> CustomersDeleted =
        Meter.CreateCounter<long>(
            name: "customers.deleted",
            unit: "customers",
            description: "Total number of customers deleted.");

    public static readonly Histogram<double> CustomerOperationDuration =
        Meter.CreateHistogram<double>(
            name: "customers.operation.duration",
            unit: "ms",
            description: "Duration of customer service operations in milliseconds.");
}