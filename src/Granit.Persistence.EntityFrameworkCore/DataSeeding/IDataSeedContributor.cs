namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Contributes seed data during application startup.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface in each module to seed reference data (roles, permissions,
/// lookup tables, legal document types, etc.). Contributors are resolved from DI and
/// executed sequentially by <see cref="IDataSeeder"/>.
/// </para>
/// <para>
/// Implementations must be <b>idempotent</b>: calling <see cref="SeedAsync"/> multiple
/// times with the same <see cref="DataSeedContext"/> must produce the same result.
/// Use upsert logic or existence checks, not blind inserts.
/// </para>
/// <para>
/// Register implementations as transient:
/// <code>
/// services.AddTransient&lt;IDataSeedContributor, MyModuleSeedContributor&gt;();
/// </code>
/// </para>
/// </remarks>
public interface IDataSeedContributor
{
    /// <summary>
    /// Seeds data for the given context.
    /// </summary>
    /// <param name="context">
    /// Seeding context containing the tenant identifier and additional properties.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default);
}
