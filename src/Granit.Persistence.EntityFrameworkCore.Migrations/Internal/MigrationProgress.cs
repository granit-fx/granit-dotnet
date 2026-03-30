namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// System entity that tracks the progress of a migration cycle's data backfill phase.
/// Mapped to the <c>data_migration_progress</c> table in the system schema.
/// </summary>
internal sealed class MigrationProgress
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Unique identifier of the migration cycle (e.g., <c>"patient-fullname-v2"</c>).</summary>
    public string CycleId { get; set; } = string.Empty;

    /// <summary>Current phase of the Expand &amp; Contract cycle.</summary>
    public MigrationPhase Phase { get; set; }

    /// <summary>Current execution status of the data backfill job.</summary>
    public MigrationStatus Status { get; set; }

    /// <summary>Total number of rows migrated so far across all batches.</summary>
    public long ProcessedRows { get; set; }

    /// <summary>
    /// Optional total row estimate for progress monitoring.
    /// Never computed automatically — set manually if needed.
    /// A <c>COUNT(*)</c> on a large ISO 27001 table can take several seconds and partially lock the table.
    /// </summary>
    public long? TotalRows { get; set; }

    /// <summary>
    /// JSON-serialized cursor that identifies the start of the next batch.
    /// Allows resuming after a crash without reprocessing already-migrated rows.
    /// </summary>
    public string? LastCursor { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenant applications.
    /// <c>null</c> for single-tenant applications.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Last error message recorded when <see cref="Status"/> is <see cref="MigrationStatus.Failed"/>.</summary>
    public string? Error { get; set; }

    /// <summary>Timestamp when the Migrate phase started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Timestamp when all rows were successfully migrated.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
