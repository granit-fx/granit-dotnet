using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Persistent checkpoint row for the <c>Granit.Indexing.BackgroundJobs</c> rebuild
/// service. One row per <c>(TenantId, SourceName)</c> tuple; the
/// <see cref="LastProcessedKey"/> is the source-supplied resume cursor stored as its
/// string round-trip so the same row shape works for any <c>TKey</c>.
/// </summary>
/// <remarks>
/// <para>String round-trip rather than a generic column type:</para>
/// <list type="bullet">
///   <item>EF Core can't map a generic <c>TKey</c> to a single physical column type
///         in a non-generic entity without per-key migrations.</item>
///   <item>Hosts typically use <c>Guid</c> / <c>long</c> / <c>string</c> keys whose
///         <c>.ToString()</c> ↔ <c>Parse()</c> is lossless.</item>
///   <item>The checkpoint table is tiny (one row per rebuild target) so the cost of
///         the string conversion is negligible.</item>
/// </list>
/// <para>
/// <b>Sensitive data.</b> <see cref="LastProcessedKey"/> carries an opaque
/// resume cursor whose content is host-defined. For hosts using <c>Guid</c> /
/// <c>long</c> keys it is non-personal, but a host using <c>IIndexedEntrySource&lt;string&gt;</c>
/// with usernames or e-mails would write those identifiers verbatim into the row.
/// The property is therefore annotated <see cref="SensitiveDataAttribute"/> so
/// audit interceptors, log redaction, MCP output sanitisers and GDPR exports
/// treat it conservatively by default.
/// </para>
/// </remarks>
public sealed class IndexingRebuildCheckpointRow : IConcurrencyAware
{
    /// <summary>Tenant scope. <c>null</c> for ops-driven cross-tenant rebuilds.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Source identifier — <see cref="IIndexedEntrySource{TKey}.Name"/>.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// String round-trip of the last successfully processed <c>TKey</c>. May contain
    /// host-defined identifiers — treat as sensitive by default.
    /// </summary>
    [SensitiveData(Level = Sensitivity.Internal, Mode = SensitiveDataMode.Mask)]
    public string LastProcessedKey { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last checkpoint write — informational, surfaced on metrics.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic-concurrency token (auto-managed by <c>ConcurrencyStampInterceptor</c>
    /// and wired by <c>ApplyGranitConventions</c>). A second worker racing on the same
    /// <c>(TenantId, SourceName)</c> tuple loses with
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>, which
    /// <see cref="Granit.Indexing.EntityFrameworkCore.Internal.EfRebuildCheckpointStore{TKey}"/>
    /// rethrows as <see cref="Granit.Indexing.BackgroundJobs.Exceptions.RebuildAlreadyInProgressException"/>
    /// so Wolverine can dead-letter the duplicate.
    /// </summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
