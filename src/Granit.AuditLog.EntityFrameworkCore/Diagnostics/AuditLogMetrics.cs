using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.AuditLog.EntityFrameworkCore.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the audit log module.
/// Meter: <c>Granit.AuditLog</c>.
/// </summary>
public sealed class AuditLogMetrics
{
    public const string MeterName = "Granit.AuditLog";

    private readonly Counter<long> _entriesPersisted;
    private readonly Counter<long> _entriesPurged;
    private readonly Counter<long> _captureErrors;

    public AuditLogMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entriesPersisted = meter.CreateCounter<long>(
            "granit.auditlog.entries.persisted",
            description: "Number of audit log entries successfully persisted.");

        _entriesPurged = meter.CreateCounter<long>(
            "granit.auditlog.entries.purged",
            description: "Number of audit log entries purged by the cleanup worker.");

        _captureErrors = meter.CreateCounter<long>(
            "granit.auditlog.capture.errors",
            description: "Number of errors during audit change tracking capture.");
    }

    public void RecordPersisted(long count, string? tenantId) =>
        _entriesPersisted.Add(count, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordPurged(long count, string category) =>
        _entriesPurged.Add(count, new TagList
        {
            { "category", category },
        });

    public void RecordCaptureError(string? tenantId) =>
        _captureErrors.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });
}
