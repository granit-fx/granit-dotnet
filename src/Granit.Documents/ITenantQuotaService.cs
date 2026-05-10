using Granit.Documents.Domain;

namespace Granit.Documents;

/// <summary>
/// Service abstraction over <see cref="TenantStorageQuota"/> bookkeeping (F7.1).
/// </summary>
/// <remarks>
/// <para>
/// All write operations issue a single atomic SQL <c>UPDATE</c> via
/// <c>ExecuteUpdateAsync</c> so concurrent uploads from the same tenant don't lose
/// increments to a read-then-write race. The matching row is created lazily on first
/// use through <see cref="EnsureTenantQuotaAsync"/>, mirroring the tenant-root folder
/// bootstrap from F2.2.
/// </para>
/// </remarks>
public interface ITenantQuotaService
{
    /// <summary>
    /// Ensures a quota row exists for the given tenant, seeding
    /// <see cref="TenantStorageQuota.LimitBytes"/> from
    /// <c>GranitDocumentsOptions.DefaultTenantQuotaBytes</c>. Concurrent calls converge
    /// on a single row via the unique index on <c>(TenantId)</c>.
    /// </summary>
    Task<TenantStorageQuota> EnsureTenantQuotaAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments <see cref="TenantStorageQuota.UsageBytes"/> by
    /// <paramref name="delta"/>. Creates the row if missing.
    /// </summary>
    Task IncrementAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements <see cref="TenantStorageQuota.UsageBytes"/> by
    /// <paramref name="delta"/>, clamping at zero. No-op when the row is missing —
    /// decrementing a non-existent quota means the tenant never wrote anything.
    /// </summary>
    Task DecrementAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default);

    /// <summary>Returns the current quota row for the tenant, or <c>null</c> when not yet created.</summary>
    Task<TenantStorageQuota?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves <paramref name="delta"/> bytes against the tenant's quota: the
    /// underlying SQL is a conditional <c>UPDATE … WHERE UsageBytes + @delta &lt;= LimitBytes</c>
    /// and returns <c>true</c> only when the reservation succeeded. Used by upload finalize
    /// (F7.2) so concurrent finalisations from the same tenant cannot collectively exceed
    /// the limit through a read-then-increment race.
    /// </summary>
    /// <remarks>
    /// Lazy-creates the row on first use. <paramref name="delta"/> must be non-negative;
    /// zero is a no-op that always returns <c>true</c>.
    /// </remarks>
    Task<bool> TryReserveAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments <see cref="TenantStorageQuota.RenditionUsageBytes"/> by
    /// <paramref name="delta"/>. Mirrors <see cref="IncrementAsync"/> for the rendition-side
    /// counter — never gated by <see cref="TenantStorageQuota.LimitBytes"/>, since renditions
    /// are framework-generated derivatives and rejecting them would break feature parity.
    /// </summary>
    Task IncrementRenditionAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements <see cref="TenantStorageQuota.RenditionUsageBytes"/> by
    /// <paramref name="delta"/>, clamping at zero. Invoked when renditions are dropped
    /// (cascading permanent-delete of the parent document, or when an old rendition is
    /// superseded by a fresh one for a newer version).
    /// </summary>
    Task DecrementRenditionAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default);
}
