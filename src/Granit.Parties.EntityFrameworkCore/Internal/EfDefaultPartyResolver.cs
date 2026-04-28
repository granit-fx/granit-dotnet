using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IDefaultPartyResolver"/>. Resolves the
/// host-scoped <see cref="Party"/> linked to a tenant via the reserved
/// <see cref="PartyExternalProviderNames.Tenant"/> external mapping, with the
/// multi-tenant query filter disabled so the lookup succeeds in any active scope.
/// </summary>
internal sealed class EfDefaultPartyResolver(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IDataFilter dataFilter) : IDefaultPartyResolver
{
    public async Task<Party?> GetDefaultForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must not be empty.", nameof(tenantId));
        }

        string externalId = tenantId.ToString();

        // The tenant↔party reverse-link is by definition host-scoped (TenantId == null),
        // so always disable the multi-tenant filter — even tenant-context callers must be
        // able to resolve their own representative host-scoped party.
        using IDisposable bypass = dataFilter.Disable<IMultiTenant>();
        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.Parties
            .Include(c => c.Addresses)
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .Include(c => c.ExternalMappings)
            .FirstOrDefaultAsync(
                c => c.TenantId == null
                  && c.ExternalMappings.Any(m =>
                        m.ProviderName == PartyExternalProviderNames.Tenant
                        && m.ExternalId == externalId),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
