namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Persistent checkpoint row for the <c>Granit.Indexing.BackgroundJobs</c> rebuild
/// service. One row per <c>(TenantId, SourceName)</c> tuple; the
/// <see cref="LastProcessedKey"/> is the source-supplied resume cursor stored as its
/// string round-trip so the same row shape works for any <c>TKey</c>.
/// </summary>
/// <remarks>
/// String round-trip rather than a generic column type:
/// <list type="bullet">
///   <item>EF Core can't map a generic <c>TKey</c> to a single physical column type
///         in a non-generic entity without per-key migrations.</item>
///   <item>Hosts typically use <c>Guid</c> / <c>long</c> / <c>string</c> keys whose
///         <c>.ToString()</c> ↔ <c>Parse()</c> is lossless.</item>
///   <item>The checkpoint table is tiny (one row per rebuild target) so the cost of
///         the string conversion is negligible.</item>
/// </list>
/// </remarks>
public sealed class IndexingRebuildCheckpointRow
{
    /// <summary>Tenant scope. <c>null</c> for ops-driven cross-tenant rebuilds.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Source identifier — <see cref="IIndexedEntrySource{TKey}.Name"/>.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>String round-trip of the last successfully processed <c>TKey</c>.</summary>
    public string LastProcessedKey { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last checkpoint write — informational, surfaced on metrics.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
