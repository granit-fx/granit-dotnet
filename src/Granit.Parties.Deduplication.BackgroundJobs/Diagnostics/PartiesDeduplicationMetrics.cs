using System.Diagnostics.Metrics;

namespace Granit.Parties.Deduplication.BackgroundJobs.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the recurring Party duplicate-detection scan.
/// Meter: <c>Granit.Parties.Deduplication</c>.
/// </summary>
public sealed class PartiesDeduplicationMetrics
{
    /// <summary>The meter name used for all dedup-scan metrics.</summary>
    public const string MeterName = "Granit.Parties.Deduplication";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _candidatesUpserted;
    private readonly Counter<long> _candidatesDismissedSkipped;
    private readonly Histogram<double> _scanDurationSeconds;

    /// <summary>Initialises the metrics from the application's <see cref="IMeterFactory"/>.</summary>
    public PartiesDeduplicationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _candidatesUpserted = meter.CreateCounter<long>(
            "granit.parties.deduplication.scan.candidates_upserted",
            description: "Number of duplicate candidate pairs inserted or refreshed by the recurring scan.");

        _candidatesDismissedSkipped = meter.CreateCounter<long>(
            "granit.parties.deduplication.scan.candidates_dismissed_skipped",
            description: "Number of detected pairs that were skipped because they had been dismissed by an admin.");

        _scanDurationSeconds = meter.CreateHistogram<double>(
            "granit.parties.deduplication.scan.duration_seconds",
            unit: "s",
            description: "Wall-clock duration of one tenant's full duplicate scan.");
    }

    /// <summary>Records that <paramref name="count"/> candidate pairs were upserted for <paramref name="tenantId"/>.</summary>
    public void RecordUpserted(Guid? tenantId, int count) =>
        _candidatesUpserted.Add(count, new KeyValuePair<string, object?>(TenantIdTag, Tag(tenantId)));

    /// <summary>Records that <paramref name="count"/> candidate pairs were skipped because dismissed.</summary>
    public void RecordDismissedSkipped(Guid? tenantId, int count) =>
        _candidatesDismissedSkipped.Add(count, new KeyValuePair<string, object?>(TenantIdTag, Tag(tenantId)));

    /// <summary>Records the wall-clock duration of one tenant's scan.</summary>
    public void RecordScanDuration(Guid? tenantId, TimeSpan duration) =>
        _scanDurationSeconds.Record(duration.TotalSeconds, new KeyValuePair<string, object?>(TenantIdTag, Tag(tenantId)));

    private static string Tag(Guid? tenantId) =>
        tenantId is { } t ? t.ToString() : GlobalTenant;
}
