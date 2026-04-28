using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.Parties.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IDefaultPartySeeder"/>. Creates a host-scoped
/// <see cref="Party"/> with the reserved <see cref="PartyExternalProviderNames.Tenant"/>
/// external mapping pointing back to the tenant identifier. Idempotent — a second call
/// returns the party created by the first.
/// </summary>
internal sealed class EfDefaultPartySeeder(
    IDefaultPartyResolver resolver,
    IDbContextFactory<PartiesDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IDataFilter dataFilter) : IDefaultPartySeeder
{
    public async Task<Party> SeedForTenantAsync(
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

        Party? existing = await resolver
            .GetDefaultForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var party = Party.Create(
            id: guidGenerator.Create(),
            tenantId: null,
            kind: PartyKind.Company,
            name: tenantName,
            defaultCurrency: defaultCurrency);
        party.AddExternalMapping(
            guidGenerator.Create(),
            PartyExternalProviderNames.Tenant,
            tenantId.ToString());

        // Insert as host-scoped (TenantId == null). The multi-tenant filter must be
        // bypassed because the seeder typically runs in a tenant context (the tenant
        // that was just created) where the filter would otherwise reject TenantId == null
        // rows on the read-after-write that EF Core performs.
        using IDisposable bypass = dataFilter.Disable<IMultiTenant>();
        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Parties.Add(party);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return party;
    }
}
