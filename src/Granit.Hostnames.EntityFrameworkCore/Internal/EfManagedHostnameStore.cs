using Granit.Hostnames.Contracts;
using Granit.Hostnames.Diagnostics;
using Granit.Hostnames.Domain;
using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IManagedHostnameReader"/> and
/// <see cref="IManagedHostnameWriter"/>.
/// </summary>
/// <remarks>
/// All tenant-scoped reads are implicitly filtered to the current tenant via the
/// <see cref="Granit.Domain.IMultiTenant"/> query filter on <see cref="HostnamesDbContext"/>.
/// Host-lookup queries that must see across tenants (e.g. <see cref="FindByHostAsync"/>)
/// bypass the tenant filter explicitly with
/// <c>IgnoreQueryFilters([GranitFilterNames.MultiTenant])</c>.
/// </remarks>
internal sealed class EfManagedHostnameStore(
    IDbContextFactory<HostnamesDbContext> contextFactory,
    ICurrentTenant currentTenant,
    HostnamesMetrics metrics)
    : EfStoreBase<ManagedHostname, HostnamesDbContext>(contextFactory, currentTenant),
      IManagedHostnameReader,
      IManagedHostnameWriter
{
    // ── IManagedHostnameReader ──────────────────────────────────────────

    /// <inheritdoc/>
    public Task<ManagedHostname?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<ManagedHostname?> FindByHostAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        Hostname? hostnameValue;
        try
        {
            hostnameValue = Hostname.Create(host);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<ManagedHostname?>(null);
        }

        // Host lookup is tenant-agnostic — the same hostname cannot belong to two tenants
        // (unique index), and the caller may not have a tenant context.
        return ReadAsync(
            db => db.ManagedHostnames
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .FirstOrDefaultAsync(h => h.Host == hostnameValue, cancellationToken),
            cancellationToken);
    }

    private const int MaxListByOwnerResults = 500;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ManagedHostname>> ListByOwnerAsync(
        string ownerType,
        Guid ownerId,
        int maxResults = MaxListByOwnerResults,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<ManagedHostname>()
                .Where(h => h.OwnerType == ownerType && h.OwnerId == ownerId)
                // Order by the whole Host value object — its ValueConverter round-trips the whole
                // value to a single column, so EF can translate ORDER BY over `h.Host`. Reaching
                // into `h.Host.Value` is NOT translatable (the converter has no member mapping for
                // `.Value`) and throws "could not be translated" at query time.
                .OrderBy(h => (object)h.Host)
                .Limit(Math.Min(maxResults, MaxListByOwnerResults)),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ManagedHostname>> ListDueForVerificationAsync(
        DateTimeOffset now,
        int batchSize = 100,
        CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<ManagedHostname>>(
            async db =>
            {
                // Poller query: status ∈ {Verifying, Error} AND NextCheckAt ≤ now (non-null).
                // Bypass tenant filter — the poller is a system job, not tenant-scoped.
                List<ManagedHostname> results = await db.ManagedHostnames
                    .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                    .Where(h =>
                        (h.Status == HostnameStatus.Verifying || h.Status == HostnameStatus.Error) &&
                        h.NextCheckAt != null && h.NextCheckAt <= now)
                    .OrderBy(h => h.NextCheckAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return results;
            },
            cancellationToken);

    // ── IManagedHostnameWriter ──────────────────────────────────────────

    /// <inheritdoc/>
    public new async Task AddAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default)
    {
        await base.AddAsync(hostname, cancellationToken).ConfigureAwait(false);
        metrics.RecordCreated(hostname.TenantId);
    }

    /// <inheritdoc/>
    public new Task UpdateAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default) =>
        base.UpdateAsync(hostname, cancellationToken);

    /// <inheritdoc/>
    public new async Task DeleteAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default)
    {
        await base.DeleteAsync(hostname, cancellationToken).ConfigureAwait(false);
        metrics.RecordDeleted(hostname.TenantId);
    }
}
