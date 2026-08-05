namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Selects which pipeline <see cref="IGranitMigrationRunner.RunAsync"/> executes.
/// Both modes share the same distributed lock, timeout, and exit-code semantics —
/// there is deliberately no second orchestration path.
/// </summary>
public enum MigrationRunMode
{
    /// <summary>
    /// Full migration pipeline: EF Core schema migrations for every migratable module
    /// in topological order, external (non-EF) stores, and optional data seeding.
    /// This is the <c>--migrate</c> CLI mode.
    /// </summary>
    Full,

    /// <summary>
    /// Resume-only pipeline: dispatches one batch command per pending or in-progress
    /// data-migration cycle so interrupted cycles continue from their stored cursor.
    /// This is the startup mode triggered by <c>MigrationStartupService</c>.
    /// </summary>
    ResumeBatches,
}
