using Granit.Persistence.EntityFrameworkCore.Migrations;

namespace Granit.Persistence.EntityFrameworkCore.Hosting;

/// <summary>
/// Migrates an external (non-EF Core) storage during <c>--migrate</c> mode.
/// </summary>
/// <remarks>
/// Implementations are discovered via DI and invoked by <see cref="IGranitMigrationRunner"/>
/// after EF Core migrations complete. This allows infrastructure stores (e.g., Wolverine
/// message storage) to participate in the centralized migration pipeline without coupling
/// the runner to specific technologies.
/// </remarks>
public interface IExternalStoreMigrator
{
    /// <summary>
    /// Human-readable name for log output (e.g., "Wolverine message storage").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies pending schema changes to the external store.
    /// Must be idempotent — safe to call multiple times.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
