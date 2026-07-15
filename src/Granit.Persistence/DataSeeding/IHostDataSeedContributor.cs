namespace Granit.Persistence.DataSeeding;

/// <summary>
/// Contributes seed data that belongs to the host (shared) database context.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are executed once by <see cref="IDataSeeder.SeedHostAsync"/> in a single
/// DI scope without any tenant context active. Typical use cases include seeding tenants
/// themselves, shared roles, OpenIddict applications, or any host-level reference data.
/// </para>
/// <para>
/// Register implementations as transient:
/// <c>services.AddTransient&lt;IHostDataSeedContributor, MyContributor&gt;();</c>
/// </para>
/// <para>
/// All contributors must be <b>idempotent</b> — calling <see cref="SeedAsync"/> multiple
/// times must produce the same result (use upsert logic or existence checks).
/// </para>
/// </remarks>
public interface IHostDataSeedContributor
{
    /// <summary>
    /// Seeds host-level data.
    /// </summary>
    /// <param name="context">Seeding context (always host-level, <see cref="DataSeedContext.TenantId"/> is <c>null</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default);
}
