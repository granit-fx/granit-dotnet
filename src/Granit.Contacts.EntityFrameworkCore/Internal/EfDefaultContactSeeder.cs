using Granit.Contacts.Domain;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IDefaultContactSeeder"/>. Creates a host-scoped
/// <see cref="Contact"/> with the reserved <see cref="ContactExternalProviderNames.Tenant"/>
/// external mapping pointing back to the tenant identifier. Idempotent — a second call
/// returns the contact created by the first.
/// </summary>
internal sealed class EfDefaultContactSeeder(
    IDefaultContactResolver resolver,
    IDbContextFactory<ContactsDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IDataFilter dataFilter) : IDefaultContactSeeder
{
    public async Task<Contact> SeedForTenantAsync(
        Guid tenantId,
        string tenantName,
        string defaultCurrency = "EUR",
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must not be empty.", nameof(tenantId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);

        Contact? existing = await resolver
            .GetDefaultForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var contact = Contact.Create(
            id: guidGenerator.Create(),
            tenantId: null,
            kind: ContactKind.Company,
            name: tenantName,
            defaultCurrency: defaultCurrency);
        contact.AddExternalMapping(
            guidGenerator.Create(),
            ContactExternalProviderNames.Tenant,
            tenantId.ToString());

        // Insert as host-scoped (TenantId == null). The multi-tenant filter must be
        // bypassed because the seeder typically runs in a tenant context (the tenant
        // that was just created) where the filter would otherwise reject TenantId == null
        // rows on the read-after-write that EF Core performs.
        using IDisposable bypass = dataFilter.Disable<IMultiTenant>();
        await using ContactsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return contact;
    }
}
