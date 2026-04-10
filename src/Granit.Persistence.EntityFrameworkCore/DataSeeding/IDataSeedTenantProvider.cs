namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Provides the list of active tenant identifiers for tenant-scoped data seeding.
/// </summary>
/// <remarks>
/// <para>
/// Used by <see cref="IDataSeeder"/> to iterate tenants when executing
/// <see cref="ITenantDataSeedContributor"/> instances. When no implementation is registered
/// (single-tenant applications), tenant contributors execute once without tenant context.
/// </para>
/// <para>
/// The default implementation in <c>Granit.Persistence.EntityFrameworkCore.Migrations</c>
/// delegates to <c>ITenantEnumerator</c>. Applications with custom tenant stores can
/// provide their own implementation.
/// </para>
/// </remarks>
public interface IDataSeedTenantProvider
{
    /// <summary>
    /// Returns the identifiers of all active tenants that should be seeded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of tenant identifiers.</returns>
    IAsyncEnumerable<Guid> GetTenantIdsAsync(CancellationToken cancellationToken = default);
}
