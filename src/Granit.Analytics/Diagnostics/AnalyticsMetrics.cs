using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Analytics.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit.Analytics module.
/// Meter: <c>Granit.Analytics</c>.
/// </summary>
public sealed class AnalyticsMetrics
{
    /// <summary>The OpenTelemetry meter name.</summary>
    public const string MeterName = "Granit.Analytics";

    private readonly Counter<long> _metricsExecuted;
    private readonly Histogram<double> _metricExecutionDuration;
    private readonly Counter<long> _emptySetEncountered;

    /// <summary>Creates a new <see cref="AnalyticsMetrics"/> instance.</summary>
    /// <param name="meterFactory">The meter factory.</param>
    public AnalyticsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        Meter meter = meterFactory.Create(MeterName);

        _metricsExecuted = meter.CreateCounter<long>(
            "granit.analytics.metric.executed",
            description: "Number of metric executions (Count/Sum/Avg/Min/Max).");

        _metricExecutionDuration = meter.CreateHistogram<double>(
            "granit.analytics.metric.duration",
            unit: "s",
            description: "Duration of metric execution in seconds.");

        _emptySetEncountered = meter.CreateCounter<long>(
            "granit.analytics.metric.empty_set",
            description: "Number of metric executions that returned no-data (empty set on Avg/Min/Max).");
    }

    /// <summary>Records a metric execution.</summary>
    public void RecordMetricExecuted(string? tenantId, string metricName, string aggregation)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("metric_name", metricName),
            new("aggregation", aggregation),
        ];
        _metricsExecuted.Add(1, tags);
    }

    /// <summary>Records the duration of a metric execution.</summary>
    public void RecordMetricDuration(string? tenantId, string metricName, string aggregation, double durationSeconds)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("metric_name", metricName),
            new("aggregation", aggregation),
        ];
        _metricExecutionDuration.Record(durationSeconds, tags);
    }

    /// <summary>Records an empty-set encounter (Avg/Min/Max returned null).</summary>
    public void RecordEmptySet(string? tenantId, string metricName, string aggregation)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("metric_name", metricName),
            new("aggregation", aggregation),
        ];
        _emptySetEncountered.Add(1, tags);
    }
}
