namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Defines the three phases of the Expand &amp; Contract zero-downtime migration pattern.
/// </summary>
public enum MigrationPhase
{
    /// <summary>
    /// Add the new column (nullable or with a default value).
    /// Application writes to both old and new columns; reads from the old column only.
    /// </summary>
    Expand,

    /// <summary>
    /// Background batch job backfills the new column from the old one.
    /// No schema changes occur during this phase.
    /// </summary>
    Migrate,

    /// <summary>
    /// Remove the old column. Application reads and writes only the new column.
    /// Requires <see cref="MigrationCycleAttribute"/> annotating the EF Core migration class.
    /// </summary>
    Contract,
}
