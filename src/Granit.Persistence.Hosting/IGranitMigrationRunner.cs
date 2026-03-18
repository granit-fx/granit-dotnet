namespace Granit.Persistence.Hosting;

/// <summary>
/// Orchestrates EF Core migrations across all <see cref="IMigratableModule{TContext}"/> modules
/// in dependency order, with distributed locking, retry, and optional data seeding.
/// </summary>
public interface IGranitMigrationRunner
{
    /// <summary>
    /// Applies all pending EF Core migrations in topological order, optionally seeds data,
    /// and returns an exit code.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the entire operation.</param>
    /// <returns><c>0</c> on success, non-zero on failure.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
