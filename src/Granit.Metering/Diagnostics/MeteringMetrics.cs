using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Metering.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the metering module.
/// Meter: <c>Granit.Metering</c>.
/// </summary>
public sealed class MeteringMetrics
{
    /// <summary>The meter name used for all metering metrics.</summary>
    public const string MeterName = "Granit.Metering";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";
    private const string MeterDefinitionIdTag = "meter_definition_id";

    private readonly Counter<long> _eventsRecorded;
    private readonly Counter<long> _eventsDeduplicated;
    private readonly Counter<long> _aggregationsCompleted;
    private readonly Counter<long> _quotaThresholdsReached;
    private readonly Counter<long> _quotasExceeded;
    private readonly Counter<long> _recomputesExecuted;

    /// <summary>Initializes metering metrics using the specified meter factory.</summary>
    public MeteringMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _eventsRecorded = meter.CreateCounter<long>(
            "granit.metering.event.recorded",
            description: "Number of meter events recorded.");

        _eventsDeduplicated = meter.CreateCounter<long>(
            "granit.metering.event.deduplicated",
            description: "Number of duplicate meter events silently ignored.");

        _aggregationsCompleted = meter.CreateCounter<long>(
            "granit.metering.aggregation.completed",
            description: "Number of aggregation batches completed.");

        _quotaThresholdsReached = meter.CreateCounter<long>(
            "granit.metering.quota.threshold_reached",
            description: "Number of quota threshold alerts triggered.");

        _quotasExceeded = meter.CreateCounter<long>(
            "granit.metering.quota.exceeded",
            description: "Number of quota exceeded alerts triggered.");

        _recomputesExecuted = meter.CreateCounter<long>(
            "granit.metering.recomputes.executed",
            description: "Number of on-demand UsageAggregate recompute operations executed.");
    }

    /// <summary>Records a meter event insertion.</summary>
    public void RecordEvent(string? tenantId, Guid meterDefinitionId)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { MeterDefinitionIdTag, meterDefinitionId.ToString() },
        };
        _eventsRecorded.Add(1, tags);
    }

    /// <summary>Records a deduplicated (ignored) event.</summary>
    public void RecordDeduplicated(string? tenantId, Guid meterDefinitionId)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { MeterDefinitionIdTag, meterDefinitionId.ToString() },
        };
        _eventsDeduplicated.Add(1, tags);
    }

    /// <summary>Records a completed aggregation batch.</summary>
    public void RecordAggregation(string? tenantId, Guid meterDefinitionId, long eventCount)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { MeterDefinitionIdTag, meterDefinitionId.ToString() },
        };
        _aggregationsCompleted.Add(eventCount, tags);
    }

    /// <summary>Records a quota threshold alert.</summary>
    public void RecordQuotaThreshold(string? tenantId, Guid meterDefinitionId)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { MeterDefinitionIdTag, meterDefinitionId.ToString() },
        };
        _quotaThresholdsReached.Add(1, tags);
    }

    /// <summary>Records a quota exceeded alert.</summary>
    public void RecordQuotaExceeded(string? tenantId, Guid meterDefinitionId)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { MeterDefinitionIdTag, meterDefinitionId.ToString() },
        };
        _quotasExceeded.Add(1, tags);
    }

    /// <summary>Records a successful on-demand recompute over a window.</summary>
    public void RecordRecompute(string? tenantId, Guid meterDefinitionId, long aggregatesRebuilt)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { MeterDefinitionIdTag, meterDefinitionId.ToString() },
            { "aggregates_rebuilt", aggregatesRebuilt },
        };
        _recomputesExecuted.Add(1, tags);
    }
}
