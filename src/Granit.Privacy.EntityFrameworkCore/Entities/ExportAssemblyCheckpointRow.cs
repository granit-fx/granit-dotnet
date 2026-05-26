using Granit.Domain;

namespace Granit.Privacy.EntityFrameworkCore.Entities;

/// <summary>
/// Persistent checkpoint row for the <c>Granit.Privacy.BackgroundJobs</c> export
/// assembly service. One row per <c>(RequestId, TenantId)</c> tuple; carries the
/// resumable shard-level state so a crashed worker can pick up after the last
/// fully-committed shard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-tenant.</b> Persisted under <see cref="IMultiTenant.TenantId"/> so the
/// row lives behind the standard Privacy tenant filter. The store deliberately
/// bypasses the filter on every query and uses an explicit <c>r.TenantId == tenantId</c>
/// equality predicate — see <c>EfExportAssemblyCheckpointStore</c>'s class remarks.
/// </para>
/// <para>
/// <b>Concurrency.</b> Implements <see cref="IConcurrencyAware"/> so a duplicate
/// dispatcher racing on the same <c>(RequestId, TenantId)</c> tuple loses on
/// <c>SaveChangesAsync</c> with <c>DbUpdateConcurrencyException</c> rather than
/// silently clobbering progress.
/// </para>
/// </remarks>
public sealed class ExportAssemblyCheckpointRow : IMultiTenant, IConcurrencyAware
{
    /// <summary>Saga / export-request correlation id.</summary>
    public Guid RequestId { get; set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Index of the last shard whose multipart upload was confirmed. <c>-1</c> when
    /// the run started fresh — the next dispatch resumes from fragment 0.
    /// </summary>
    public int LastCompletedShardIndex { get; set; }

    /// <summary>
    /// Fragment-list index where the next shard should resume. Always points past
    /// the last fragment successfully appended to a committed shard.
    /// </summary>
    public int NextFragmentIndex { get; set; }

    /// <summary>
    /// Object keys of the shards already committed to blob storage, in shard-index
    /// order, JSON-encoded. Carried forward so the next run can rebuild the
    /// manifest without rescanning the bucket.
    /// </summary>
    public List<string> CompletedShardObjectKeys { get; set; } = [];

    /// <summary>UTC timestamp of the last checkpoint write — informational.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic-concurrency token (auto-managed by <c>ConcurrencyStampInterceptor</c>
    /// and wired by <c>ApplyGranitConventions</c>).
    /// </summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
