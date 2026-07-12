using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Auditing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the auditing module.
/// Meter: <c>Granit.Auditing</c>.
/// </summary>
public sealed class AuditingMetrics
{
    public const string MeterName = "Granit.Auditing";

    /// <summary>Persistence-mode tag value: audit rows ride the host transaction.</summary>
    public const string EmbeddedMode = "embedded";

    /// <summary>Persistence-mode tag value: audit rows use the isolated AuditingDbContext.</summary>
    public const string StandaloneMode = "standalone";

    private const string TenantIdTag = "tenant_id";
    private const string ModeTag = "mode";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _entriesPersisted;
    private readonly Counter<long> _entriesPurged;
    private readonly Counter<long> _entriesPseudonymized;
    private readonly Counter<long> _captureErrors;
    private readonly Histogram<double> _persistenceDuration;
    private readonly Histogram<int> _entityChangeCount;

    public AuditingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entriesPersisted = meter.CreateCounter<long>(
            "granit.auditing.entry.persisted",
            description: "Number of audit entries successfully persisted.");

        _entriesPurged = meter.CreateCounter<long>(
            "granit.auditing.entry.purged",
            description: "Number of audit entries purged by the retention cleanup job.");

        _entriesPseudonymized = meter.CreateCounter<long>(
            "granit.auditing.entry.pseudonymized",
            description: "Number of audit entries pseudonymized for GDPR right-to-erasure.");

        _captureErrors = meter.CreateCounter<long>(
            "granit.auditing.capture.errors",
            description: "Number of errors during audit change tracking capture.");

        _persistenceDuration = meter.CreateHistogram<double>(
            "granit.auditing.persistence.duration",
            unit: "ms",
            description: "Duration of audit entry persistence, tagged by pipeline mode.");

        _entityChangeCount = meter.CreateHistogram<int>(
            "granit.auditing.entry.entity_changes",
            unit: "{changes}",
            description: "Number of entity changes per persisted audit entry.");
    }

    public void RecordPersisted(long count, string? tenantId) =>
        _entriesPersisted.Add(count, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
        });

    public void RecordPurged(long count, string category, string? tenantId) =>
        _entriesPurged.Add(count, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "category", category },
        });

    public void RecordPseudonymized(long count, string? tenantId) =>
        _entriesPseudonymized.Add(count, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
        });

    public void RecordCaptureError(string? tenantId) =>
        _captureErrors.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
        });

    public void RecordPersistenceDuration(double elapsedMs, string? tenantId, string mode) =>
        _persistenceDuration.Record(elapsedMs, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { ModeTag, mode },
        });

    public void RecordEntityChangeCount(int entityChangeCount, string? tenantId) =>
        _entityChangeCount.Record(entityChangeCount, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
        });
}
