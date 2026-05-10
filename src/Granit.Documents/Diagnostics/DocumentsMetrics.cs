using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Documents.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit.Documents module.
/// Meter: <c>Granit.Documents</c>.
/// </summary>
/// <remarks>
/// Phase-1 baseline counters; richer per-operation tags (content type, grantee type,
/// permission, target type) arrive with the corresponding domain stories.
/// </remarks>
public sealed class DocumentsMetrics
{
    /// <summary>Meter name — <c>Granit.Documents</c>.</summary>
    public const string MeterName = "Granit.Documents";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _uploads;
    private readonly Counter<long> _downloads;
    private readonly Counter<long> _sharesGranted;
    private readonly Counter<long> _quotaRejected;
    private readonly Counter<long> _aclCacheHits;
    private readonly Counter<long> _aclCacheMisses;

    /// <summary>Initialises the meter and counters.</summary>
    public DocumentsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _uploads = meter.CreateCounter<long>(
            "granit.documents.upload.count",
            description: "Number of document version uploads finalised successfully.");

        _downloads = meter.CreateCounter<long>(
            "granit.documents.download.count",
            description: "Number of document downloads (signed-URL issuances).");

        _sharesGranted = meter.CreateCounter<long>(
            "granit.documents.share.granted.count",
            description: "Number of document or folder share grants issued.");

        _quotaRejected = meter.CreateCounter<long>(
            "granit.documents.quota.rejected.count",
            description: "Number of upload finalisations rejected because the tenant storage quota would be exceeded.");

        // F6.3 — ACL cache hit/miss counters. Hit ratio is computed downstream by the
        // observability pipeline (e.g. Prometheus/Grafana) as
        //   hits / (hits + misses) → granit.documents.acl.cache.hit_ratio.
        _aclCacheHits = meter.CreateCounter<long>(
            "granit.documents.acl.cache.hits",
            description: "Number of effective-ACL resolutions served from the FusionCache layer.");

        _aclCacheMisses = meter.CreateCounter<long>(
            "granit.documents.acl.cache.misses",
            description: "Number of effective-ACL resolutions that fell through to the database.");
    }

    /// <summary>Records a successful document upload (new document or new version).</summary>
    public void RecordUpload(string? tenantId) =>
        _uploads.Add(1, CreateTags(tenantId));

    /// <summary>Records a document download (one signed-URL issuance).</summary>
    public void RecordDownload(string? tenantId) =>
        _downloads.Add(1, CreateTags(tenantId));

    /// <summary>Records a share grant on a document or folder.</summary>
    public void RecordShareGranted(string? tenantId) =>
        _sharesGranted.Add(1, CreateTags(tenantId));

    /// <summary>Records an upload rejection due to tenant storage quota being exceeded.</summary>
    public void RecordQuotaRejected(string? tenantId) =>
        _quotaRejected.Add(1, CreateTags(tenantId));

    /// <summary>Records an effective-ACL cache hit.</summary>
    public void RecordAclCacheHit(string? tenantId) =>
        _aclCacheHits.Add(1, CreateTags(tenantId));

    /// <summary>Records an effective-ACL cache miss (resolution fell through to the database).</summary>
    public void RecordAclCacheMiss(string? tenantId) =>
        _aclCacheMisses.Add(1, CreateTags(tenantId));

    private static TagList CreateTags(string? tenantId) => new()
    {
        { TagTenantId, tenantId ?? DefaultTenant },
    };
}
