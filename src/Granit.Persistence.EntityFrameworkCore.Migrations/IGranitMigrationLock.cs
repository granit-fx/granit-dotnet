namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Distributed lock to prevent concurrent migration execution across multiple instances.
/// </summary>
/// <remarks>
/// Implementations must hold the lock for the entire migration duration.
/// The returned <see cref="IAsyncDisposable"/> releases the lock when disposed.
/// Returns <c>null</c> if the lock could not be acquired (another instance is migrating).
/// Both migration entry points take this lock: the CLI runner (<c>--migrate</c>, resource
/// <c>"GranitMigration"</c>) and the startup resume of batch cycles (resource
/// <c>"GranitMigrationStartup"</c>) — so N replicas never run either path concurrently.
/// </remarks>
public interface IGranitMigrationLock
{
    /// <summary>
    /// Attempts to acquire a migration lock for the given resource.
    /// </summary>
    /// <param name="resource">Lock resource identifier (typically the database name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncDisposable"/> that releases the lock when disposed,
    /// or <c>null</c> if the lock could not be acquired.
    /// </returns>
    Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken);
}
