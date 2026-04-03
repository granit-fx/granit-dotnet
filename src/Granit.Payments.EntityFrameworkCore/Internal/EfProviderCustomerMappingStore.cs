using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

/// <summary>EF Core store for provider customer mappings.</summary>
internal sealed class EfProviderCustomerMappingStore(
    IDbContextFactory<PaymentsDbContext> contextFactory) : IProviderCustomerMappingStore
{
    public async Task<ProviderCustomerMapping?> GetAsync(
        string providerName, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.ProviderCustomerMappings
            .FirstOrDefaultAsync(
                m => m.ProviderName == providerName && m.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        ProviderCustomerMapping mapping, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.ProviderCustomerMappings.Add(mapping);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
