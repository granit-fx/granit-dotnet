using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IHostnameResolver"/>.
/// </summary>
/// <remarks>
/// Host resolution is tenant-agnostic by design — the incoming host IS the signal that
/// determines which tenant to activate, so no tenant context is available yet. The query
/// always bypasses the tenant filter via
/// <c>IgnoreQueryFilters([GranitFilterNames.MultiTenant])</c> and only matches
/// <see cref="HostnameStatus.Active"/> entries.
/// </remarks>
internal sealed class EfHostnameResolver(IDbContextFactory<HostnamesDbContext> contextFactory)
    : IHostnameResolver
{
    /// <inheritdoc/>
    public async Task<ResolvedHostname?> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        Hostname hostnameValue;
        try
        {
            hostnameValue = Hostname.Create(host);
        }
        catch (ArgumentException)
        {
            return null;
        }

        await using HostnamesDbContext db =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ManagedHostname? match = await db.ManagedHostnames
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .AsNoTracking()
            .FirstOrDefaultAsync(
                h => h.Host == hostnameValue && h.Status == HostnameStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);

        return match is null
            ? null
            : new ResolvedHostname(
                match.Host.Value,
                match.OwnerType,
                match.OwnerId,
                match.TenantId,
                match.IsPrimary);
    }
}
