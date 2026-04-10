namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Orchestrates the execution of all registered data seed contributors.
/// </summary>
/// <remarks>
/// <para>
/// Resolved from DI as a singleton. Contributors are resolved from scoped service providers
/// to support scoped dependencies (e.g., <see cref="Microsoft.EntityFrameworkCore.DbContext"/>).
/// </para>
/// <para>
/// Errors thrown by individual contributors are logged but do not prevent remaining contributors
/// from executing. Only <see cref="OperationCanceledException"/> is propagated immediately.
/// </para>
/// <para>
/// The seeder supports three contributor interfaces:
/// <list type="bullet">
///   <item><see cref="IHostDataSeedContributor"/> — executed once during <see cref="SeedHostAsync"/>.</item>
///   <item><see cref="ITenantDataSeedContributor"/> — executed per tenant during <see cref="SeedTenantsAsync"/>.</item>
///   <item><see cref="IDataSeedContributor"/> (legacy) — executed in both passes for backward compatibility.</item>
/// </list>
/// </para>
/// </remarks>
public interface IDataSeeder
{
    /// <summary>
    /// Executes all registered host-level seed contributors, followed by legacy contributors
    /// with <see cref="DataSeedContext.IsHostOnly"/> set to <c>true</c>.
    /// </summary>
    /// <param name="context">Seeding context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedHostAsync(DataSeedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes legacy contributors with <see cref="DataSeedContext.IsHostOnly"/> set to <c>false</c>,
    /// then executes tenant contributors once per active tenant (with tenant context activated).
    /// </summary>
    /// <remarks>
    /// When no <see cref="IDataSeedTenantProvider"/> is registered (single-tenant applications),
    /// tenant contributors execute once without tenant context.
    /// </remarks>
    /// <param name="context">Seeding context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedTenantsAsync(DataSeedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience method that calls <see cref="SeedHostAsync"/> followed by <see cref="SeedTenantsAsync"/>.
    /// </summary>
    /// <param name="context">Seeding context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default);
}
