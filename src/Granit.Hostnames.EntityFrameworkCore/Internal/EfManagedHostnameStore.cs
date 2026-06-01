using Granit.Hostnames.Contracts;
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
    ICurrentTenant currentTenant)
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

    /// <inheritdoc/>
    public Task<IReadOnlyList<ManagedHostname>> ListByOwnerAsync(
        string ownerType,
        Guid ownerId,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<ManagedHostname>()
                .Where(h => h.OwnerType == ownerType && h.OwnerId == ownerId)
                .OrderBy(h => (object)h.Host.Value),
            cancellationToken);

    // ── IManagedHostnameWriter ──────────────────────────────────────────

    /// <inheritdoc/>
    public new Task AddAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default) =>
        base.AddAsync(hostname, cancellationToken);

    /// <inheritdoc/>
    public new Task UpdateAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default) =>
        base.UpdateAsync(hostname, cancellationToken);

    /// <inheritdoc/>
    public new Task DeleteAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default) =>
        base.DeleteAsync(hostname, cancellationToken);
}
