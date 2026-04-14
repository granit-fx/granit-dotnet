namespace Granit.Persistence.EntityFrameworkCore.Hosting;

/// <summary>
/// Provisions a newly created tenant: creates the database schema (if SchemaPerTenant),
/// runs EF Core migrations for all tenant-isolated DbContexts discovered from DI,
/// and seeds tenant-specific data via <see cref="DataSeeding.IDataSeeder.SeedTenantAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation (<see cref="Internal.AutoTenantProvisioner"/>) discovers
/// isolated DbContexts automatically via
/// <see cref="MultiTenancy.IsolatedDbContextMarker"/> singletons registered by
/// <c>AddGranitIsolatedDbContext&lt;T&gt;()</c>. All operations are idempotent:
/// <c>CREATE SCHEMA IF NOT EXISTS</c>, EF Core migration tracking, and seed
/// contributors that check for existing data before inserting.
/// </para>
/// <para>
/// Replace this registration in DI with a custom implementation to add application-specific
/// provisioning logic (e.g., external registry calls, per-tenant configuration seeding).
/// Register the replacement <b>before</b> calling <c>AddGranitMigrateSupport()</c>
/// (which uses <c>TryAddSingleton</c>).
/// </para>
/// <para>
/// <b>Cold-start provisioning</b> (<c>--migrate</c>) is handled separately by
/// <see cref="Internal.GranitMigrationRunner"/> via its post-seed re-migration pass.
/// This interface is invoked only at runtime via Wolverine when
/// <see cref="MultiTenancy.Events.TenantCreatedEvent"/> is dispatched.
/// </para>
/// </remarks>
public interface ITenantProvisioner
{
    /// <summary>
    /// Provisions the tenant with the given <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the newly created tenant.</param>
    /// <param name="tenantName">Display name (passed to <c>ICurrentTenant.Change</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProvisionAsync(Guid tenantId, string tenantName, CancellationToken cancellationToken = default);
}
