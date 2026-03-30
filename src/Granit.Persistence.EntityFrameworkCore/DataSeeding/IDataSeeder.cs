namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Orchestrates the execution of all registered <see cref="IDataSeedContributor"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Resolved from DI as a singleton. Contributors are resolved from a scoped service provider
/// to support scoped dependencies (e.g., <see cref="Microsoft.EntityFrameworkCore.DbContext"/>).
/// </para>
/// <para>
/// Errors thrown by individual contributors are logged but do not prevent remaining contributors
/// from executing. Only <see cref="OperationCanceledException"/> is propagated immediately.
/// </para>
/// </remarks>
public interface IDataSeeder
{
    /// <summary>
    /// Executes all registered seed contributors for the given context.
    /// </summary>
    /// <param name="context">Seeding context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default);
}
