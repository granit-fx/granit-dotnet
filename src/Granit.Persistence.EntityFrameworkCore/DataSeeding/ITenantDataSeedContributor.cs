namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Contributes seed data that belongs to a specific tenant context.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are executed once <b>per active tenant</b> by <see cref="IDataSeeder.SeedTenantsAsync"/>.
/// The <see cref="IDataSeeder"/> creates a dedicated DI scope for each tenant and activates the
/// tenant context (<c>ICurrentTenant.Change(tenantId)</c>) before resolving and executing
/// contributors. The <see cref="DataSeedContext.TenantId"/> is set to the current tenant ID.
/// </para>
/// <para>
/// Because the tenant context is already active, implementations should <b>not</b> call
/// <c>ICurrentTenant.Change()</c> or create their own <see cref="IServiceScope"/>.
/// Use <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> to create
/// a fresh <c>DbContext</c> connected to the correct tenant schema.
/// </para>
/// <para>
/// Register implementations as transient:
/// <c>services.AddTransient&lt;ITenantDataSeedContributor, MyContributor&gt;();</c>
/// </para>
/// <para>
/// All contributors must be <b>idempotent</b> — calling <see cref="SeedAsync"/> multiple
/// times for the same tenant must produce the same result.
/// </para>
/// </remarks>
public interface ITenantDataSeedContributor
{
    /// <summary>
    /// Seeds data for a specific tenant.
    /// </summary>
    /// <param name="context">
    /// Seeding context with <see cref="DataSeedContext.TenantId"/> set to the active tenant.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default);
}
