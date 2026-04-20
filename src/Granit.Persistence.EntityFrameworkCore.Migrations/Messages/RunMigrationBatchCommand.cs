namespace Granit.Persistence.EntityFrameworkCore.Migrations.Messages;

/// <summary>
/// Command that triggers the execution of a single data migration batch.
/// Cascaded by <see cref="Handlers.RunMigrationBatchHandler"/> (via <see cref="Granit.Commands.ICommandSender"/>)
/// until all rows have been processed.
/// </summary>
/// <param name="CycleId">Identifier of the migration cycle to execute.</param>
/// <param name="TenantId">
/// Tenant for which data is being migrated.
/// <see cref="Guid.Empty"/> for single-tenant applications.
/// </param>
/// <param name="Cursor">
/// Opaque cursor from the previous batch result. <c>null</c> on the first batch.
/// </param>
/// <param name="BatchSize">Maximum number of rows to process in this batch.</param>
public sealed record RunMigrationBatchCommand(
    string CycleId,
    Guid TenantId,
    string? Cursor,
    int BatchSize);
