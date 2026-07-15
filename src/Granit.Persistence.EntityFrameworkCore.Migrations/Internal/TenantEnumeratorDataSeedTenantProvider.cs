using Granit.Persistence.DataSeeding;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Bridges <see cref="ITenantEnumerator"/> to <see cref="IDataSeedTenantProvider"/>
/// so the <c>DataSeeder</c> can iterate tenants without depending on the
/// Migrations assembly directly.
/// </summary>
internal sealed class TenantEnumeratorDataSeedTenantProvider(
    ITenantEnumerator tenantEnumerator) : IDataSeedTenantProvider
{
    public IAsyncEnumerable<Guid> GetTenantIdsAsync(CancellationToken cancellationToken = default)
        => tenantEnumerator.GetActiveTenantIdsAsync(cancellationToken);
}
