using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.Options;

/// <summary>
/// Configuration options for the Granit.Documents module.
/// </summary>
/// <remarks>
/// Phase-1 placeholders: real wiring of these options happens in later stories
/// (F7 quota enforcement, F8 trash retention, F6 ACL cache TTL).
/// </remarks>
public sealed class GranitDocumentsOptions
{
    /// <summary>Section key in the configuration (<c>"Documents"</c>).</summary>
    public const string SectionName = "Documents";

    /// <summary>
    /// Number of days a trashed document or folder is retained before permanent
    /// deletion by the empty-trash background job (F8 / F9.2).
    /// Default: 30 days (Odoo / Google Drive parity).
    /// </summary>
    [Range(1, 365)]
    public int TrashRetentionDays { get; set; } = 30;

    /// <summary>
    /// Default per-tenant storage quota in bytes used to seed
    /// <c>TenantStorageQuota.LimitBytes</c> when a tenant first stores a document
    /// (F7.1). Hosts override per subscription tier.
    /// Default: 5 GB.
    /// </summary>
    [Range(1024L * 1024L, long.MaxValue)]
    public long DefaultTenantQuotaBytes { get; set; } = 5L * 1024L * 1024L * 1024L;

    /// <summary>
    /// Time-to-live for the effective-ACL FusionCache layer (F6.3).
    /// Lower values trade cache hit ratio for permission revocation latency.
    /// Default: 5 minutes.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:30", "01:00:00")]
    public TimeSpan AclCacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
