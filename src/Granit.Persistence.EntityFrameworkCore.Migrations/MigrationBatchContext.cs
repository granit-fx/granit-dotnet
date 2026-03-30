namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Contextual information passed to a <see cref="BatchMigrationDelegate"/> on each batch execution.
/// </summary>
/// <param name="Cursor">
/// Opaque cursor identifying the start of this batch (e.g., last processed row ID as JSON).
/// <c>null</c> on the first batch.
/// </param>
/// <param name="Size">Maximum number of rows to process in this batch.</param>
/// <param name="TenantId">
/// Tenant identifier for this batch. <see cref="Guid.Empty"/> for single-tenant applications.
/// </param>
public sealed record MigrationBatchContext(string? Cursor, int Size, Guid TenantId);
