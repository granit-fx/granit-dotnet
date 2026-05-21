namespace Granit.Persistence.EntityFrameworkCore.Migrations.Options;

/// <summary>
/// Configuration options for <c>MigrationStartupService</c>.
/// Bound from the <c>"Persistence:Migrations"</c> section in <c>appsettings.json</c>.
/// </summary>
public sealed class MigrationStartupOptions
{
    /// <summary>
    /// Configuration section name in <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "Persistence:Migrations";

    /// <summary>
    /// Default number of rows to process per batch.
    /// Used when resuming pending cycles at startup.
    /// Defaults to <c>500</c>.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 500;

    /// <summary>
    /// Maximum duration for a single migration batch before timeout.
    /// Prevents infinite hangs when a batch delegate never returns.
    /// Defaults to <c>5 minutes</c>.
    /// </summary>
    public TimeSpan BatchExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
