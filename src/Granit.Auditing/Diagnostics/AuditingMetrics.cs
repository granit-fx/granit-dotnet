using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Granit.Auditing.Messages;

namespace Granit.Auditing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the auditing module.
/// Meter: <c>Granit.Auditing</c>.
/// </summary>
public sealed class AuditingMetrics
{
    public const string MeterName = "Granit.Auditing";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _entriesPersisted;
    private readonly Counter<long> _entriesPurged;
    private readonly Counter<long> _entriesPseudonymized;
    private readonly Counter<long> _captureErrors;
    private readonly Histogram<double> _persistenceDuration;
    private readonly Counter<long> _persistenceRetries;
    private readonly Histogram<int> _batchSize;

    public AuditingMetrics(IMeterFactory meterFactory, Channel<AuditingBatch> channel)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entriesPersisted = meter.CreateCounter<long>(
            "granit.auditing.entry.persisted",
            description: "Number of audit entries successfully persisted.");

        _entriesPurged = meter.CreateCounter<long>(
            "granit.auditing.entry.purged",
            description: "Number of audit entries purged by the cleanup worker.");

        _entriesPseudonymized = meter.CreateCounter<long>(
            "granit.auditing.entry.pseudonymized",
            description: "Number of audit entries pseudonymized for GDPR right-to-erasure.");

        _captureErrors = meter.CreateCounter<long>(
            "granit.auditing.capture.errors",
            description: "Number of errors during audit change tracking capture.");

        _persistenceDuration = meter.CreateHistogram<double>(
            "granit.auditing.persistence.duration",
            unit: "ms",
            description: "Duration of audit batch persistence operations.");

        _persistenceRetries = meter.CreateCounter<long>(
            "granit.auditing.persistence.retries",
            description: "Number of audit persistence retry attempts.");

        _batchSize = meter.CreateHistogram<int>(
            "granit.auditing.batch.size",
            unit: "{changes}",
            description: "Number of entity changes per audit batch.");

        meter.CreateObservableGauge(
            "granit.auditing.channel.depth",
            () => channel.Reader.Count,
            unit: "{batches}",
            description: "Current number of audit batches waiting in the async persistence channel.");
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
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordCaptureError(string? tenantId) =>
        _captureErrors.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
        });

    public void RecordPersistenceDuration(double elapsedMs, string? tenantId) =>
        _persistenceDuration.Record(elapsedMs, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordPersistenceRetry(string? tenantId) =>
        _persistenceRetries.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordBatchSize(int entityChangeCount, string? tenantId) =>
        _batchSize.Record(entityChangeCount, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });
}
