namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// The result returned by a <see cref="BatchMigrationDelegate"/> after processing one batch.
/// </summary>
/// <param name="ProcessedCount">
/// Number of rows migrated in this batch.
/// A value of <c>0</c> combined with a <c>null</c> <paramref name="NextCursor"/> signals completion.
/// </param>
/// <param name="NextCursor">
/// Opaque cursor identifying the start of the next batch (e.g., last processed row ID as JSON).
/// <c>null</c> when there are no more rows to process.
/// </param>
public sealed record MigrationBatchResult(int ProcessedCount, string? NextCursor);
