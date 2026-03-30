namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Tracks the execution status of a migration cycle's data backfill phase.
/// </summary>
public enum MigrationStatus
{
    /// <summary>The cycle has been registered but the backfill job has not started yet.</summary>
    Pending = 0,

    /// <summary>The backfill job is actively processing batches.</summary>
    InProgress = 1,

    /// <summary>All rows have been successfully migrated.</summary>
    Completed = 2,

    /// <summary>The backfill job failed. Inspect <c>MigrationProgress.Error</c> for details.</summary>
    Failed = 3,
}
