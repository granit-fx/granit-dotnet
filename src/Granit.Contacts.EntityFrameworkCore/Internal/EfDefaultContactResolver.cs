using Granit.Contacts.Domain;
using Granit.DataFiltering;
using Granit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IDefaultContactResolver"/>. Resolves the
/// host-scoped <see cref="Contact"/> linked to a tenant via the reserved
/// <see cref="ContactExternalProviderNames.Tenant"/> external mapping, with the
/// multi-tenant query filter disabled so the lookup succeeds in any active scope.
/// </summary>
internal sealed class EfDefaultContactResolver(
    IDbContextFactory<ContactsDbContext> contextFactory,
    IDataFilter dataFilter) : IDefaultContactResolver
{
    public async Task<Contact?> GetDefaultForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must not be empty.", nameof(tenantId));
        }

        string externalId = tenantId.ToString();

        // The tenant↔contact reverse-link is by definition host-scoped (TenantId == null),
        // so always disable the multi-tenant filter — even tenant-context callers must be
        // able to resolve their own representative host-scoped contact.
        using IDisposable bypass = dataFilter.Disable<IMultiTenant>();
        await using ContactsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.Contacts
            .Include(c => c.Addresses)
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .Include(c => c.ExternalMappings)
            .FirstOrDefaultAsync(
                c => c.TenantId == null
                  && c.ExternalMappings.Any(m =>
                        m.ProviderName == ContactExternalProviderNames.Tenant
                        && m.ExternalId == externalId),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
