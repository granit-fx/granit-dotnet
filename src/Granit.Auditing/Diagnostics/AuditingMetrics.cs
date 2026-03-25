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

    private readonly Counter<long> _entriesPersisted;
    private readonly Counter<long> _entriesPurged;
    private readonly Counter<long> _captureErrors;

    public AuditingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entriesPersisted = meter.CreateCounter<long>(
            "granit.auditing.entry.persisted",
            description: "Number of audit entries successfully persisted.");

        _entriesPurged = meter.CreateCounter<long>(
            "granit.auditing.entry.purged",
            description: "Number of audit entries purged by the cleanup worker.");

        _captureErrors = meter.CreateCounter<long>(
            "granit.auditing.capture.errors",
            description: "Number of errors during audit change tracking capture.");
    }

    public void RecordPersisted(long count, string? tenantId) =>
        _entriesPersisted.Add(count, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordPurged(long count, string category, string? tenantId) =>
        _entriesPurged.Add(count, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "category", category },
        });

    public void RecordCaptureError(string? tenantId) =>
        _captureErrors.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });
}
