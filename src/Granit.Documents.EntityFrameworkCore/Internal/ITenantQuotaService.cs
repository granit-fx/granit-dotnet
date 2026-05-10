using Granit.Documents.Domain;

namespace Granit.Documents.EntityFrameworkCore.Internal;

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
}
