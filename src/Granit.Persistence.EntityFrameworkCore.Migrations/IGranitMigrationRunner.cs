namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Single orchestration entry point for all migration work: EF Core schema migrations
/// across migratable modules, external stores, data seeding, and the resumption of
/// pending data-migration cycles — always under the same distributed lock and timeout.
/// </summary>
/// <remarks>
/// The implementation lives in <c>Granit.Persistence.EntityFrameworkCore.Hosting</c>
/// (registered by <c>AddGranitMigrateSupport()</c>); the interface lives here so the
/// startup resume trigger in this package can delegate to it instead of maintaining a
/// parallel orchestration path.
/// </remarks>
public interface IGranitMigrationRunner
{
    /// <summary>
    /// Runs the selected migration pipeline and returns an exit code.
    /// </summary>
    /// <param name="mode">The pipeline to execute. Defaults to <see cref="MigrationRunMode.Full"/>.</param>
    /// <param name="cancellationToken">Cancellation token for the entire operation.</param>
    /// <returns><c>0</c> on success (including lock-not-acquired skips), non-zero on failure.</returns>
    Task<int> RunAsync(
        MigrationRunMode mode = MigrationRunMode.Full,
        CancellationToken cancellationToken = default);
}
