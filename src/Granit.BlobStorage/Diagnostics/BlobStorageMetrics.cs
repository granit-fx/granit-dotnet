using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.BlobStorage.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the blob storage module.
/// Meter: <c>Granit.BlobStorage</c>.
/// </summary>
public sealed class BlobStorageMetrics
{
    public const string MeterName = "Granit.BlobStorage";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _uploadsInitiated;
    private readonly Counter<long> _validationsCompleted;
    private readonly Counter<long> _validationsFailed;
    private readonly Counter<long> _blobsDeleted;
    private readonly Counter<long> _orphansCleaned;
    private readonly Histogram<double> _confirmDuration;

    public BlobStorageMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _uploadsInitiated = meter.CreateCounter<long>(
            "granit.blob_storage.uploads.initiated",
            description: "Number of upload tickets generated.");

        _validationsCompleted = meter.CreateCounter<long>(
            "granit.blob_storage.validations.completed",
            description: "Number of blob validations completed (valid or rejected).");

        _validationsFailed = meter.CreateCounter<long>(
            "granit.blob_storage.validations.failed",
            description: "Number of blob validations that resulted in rejection.");

        _blobsDeleted = meter.CreateCounter<long>(
            "granit.blob_storage.blobs.deleted",
            description: "Number of blobs deleted.");

        _orphansCleaned = meter.CreateCounter<long>(
            "granit.blob_storage.orphans.cleaned",
            description: "Number of orphaned blobs cleaned up.");

        _confirmDuration = meter.CreateHistogram<double>(
            "granit.blob_storage.confirm.duration",
            unit: "s",
            description: "Duration of blob confirmation pipeline in seconds.");
    }

    public void RecordUploadInitiated(string? tenantId, string container) =>
        _uploadsInitiated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "container", container },
        });

    public void RecordValidationCompleted(string? tenantId, string status, string container) =>
        _validationsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "status", status },
            { "container", container },
        });

    public void RecordValidationFailed(string? tenantId, string reason, string container) =>
        _validationsFailed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
            { "container", container },
        });

    public void RecordDeleted(string? tenantId, string container) =>
        _blobsDeleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "container", container },
        });

    public void RecordOrphanCleaned(string? tenantId) =>
        _orphansCleaned.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordConfirmDuration(string? tenantId, string status, string container, TimeSpan duration) =>
        _confirmDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "status", status },
            { "container", container },
        });
}
