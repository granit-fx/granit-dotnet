using Granit.Persistence.Migrations.Messages;
using Granit.Persistence.Migrations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.Migrations.Internal;

/// <summary>
/// Hosted service that resumes pending and in-progress migration cycles at application startup.
/// </summary>
/// <remarks>
/// <para>
/// On startup, queries all <see cref="MigrationProgress"/> rows with status
/// <see cref="MigrationStatus.Pending"/> or <see cref="MigrationStatus.InProgress"/>
/// and dispatches one <see cref="RunMigrationBatchCommand"/> per cycle per tenant via <see cref="IMigrationBatchDispatcher"/>.
/// </para>
/// <para>
/// If <see cref="ITenantEnumerator"/> yields tenant identifiers (Tenant-per-Schema or
/// Tenant-per-Database topologies), one command is dispatched per tenant per cycle,
/// using the stored cursor for resumption. Otherwise (single-tenant or Shared DB),
/// one command per progress row is dispatched, mapping a <c>null</c> tenant identifier to
/// <see cref="Guid.Empty"/>.
/// </para>
/// <para>
/// Exceptions are caught and logged at <c>Error</c> level without blocking application startup.
/// </para>
/// </remarks>
internal sealed partial class MigrationStartupService(
    IDbContextFactory<MigrationProgressDbContext> progressFactory,
    ITenantEnumerator tenantEnumerator,
    IMigrationBatchDispatcher dispatcher,
    IOptions<MigrationStartupOptions> options,
    ILogger<MigrationStartupService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ResumeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStartupError(ex);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ResumeAsync(CancellationToken cancellationToken)
    {
        await using MigrationProgressDbContext db = await progressFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // EnsureCreatedAsync is a no-op when the database already has tables (from host migrations).
        // Use the relational database creator to create only missing tables from this DbContext model.
        await EnsureProgressTableAsync(db, cancellationToken).ConfigureAwait(false);

        List<MigrationProgress> pending = await db.MigrationProgresses
            .Where(p => p.Status == MigrationStatus.Pending || p.Status == MigrationStatus.InProgress)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            LogNoPendingCycles();
            return;
        }

        List<Guid> tenantIds = [];
        await foreach (Guid tenantId in tenantEnumerator.GetActiveTenantIdsAsync(cancellationToken))
        {
            tenantIds.Add(tenantId);
        }

        int batchSize = options.Value.DefaultBatchSize;
        List<RunMigrationBatchCommand> commands = BuildCommands(pending, tenantIds, batchSize);

        await dispatcher.DispatchAsync(commands, cancellationToken).ConfigureAwait(false);

        LogCommandsDispatched(commands.Count);
    }

    private static List<RunMigrationBatchCommand> BuildCommands(
        List<MigrationProgress> pending,
        List<Guid> tenantIds,
        int batchSize)
    {
        List<RunMigrationBatchCommand> commands = [];

        if (tenantIds.Count > 0)
        {
            // Tenant-per-Schema or Tenant-per-Database: dispatch one command per tenant per cycle.
            // Use the stored cursor to resume from where the previous run left off.
            ILookup<string, MigrationProgress> byCycle = pending.ToLookup(p => p.CycleId);

            foreach (IGrouping<string, MigrationProgress> group in byCycle)
            {
                ILookup<Guid?, MigrationProgress> rowByTenant = group.ToLookup(p => p.TenantId);

                foreach (Guid tenantId in tenantIds)
                {
                    string? cursor = rowByTenant[tenantId].FirstOrDefault()?.LastCursor;
                    commands.Add(new RunMigrationBatchCommand(group.Key, tenantId, cursor, batchSize));
                }
            }
        }
        else
        {
            // Single-tenant or Shared DB: one command per progress row.
            // TenantId = null in the row maps to Guid.Empty in the command.
            foreach (MigrationProgress row in pending)
            {
                Guid tenantId = row.TenantId ?? Guid.Empty;
                commands.Add(new RunMigrationBatchCommand(row.CycleId, tenantId, row.LastCursor, batchSize));
            }
        }

        return commands;
    }

    /// <summary>
    /// Creates the <c>data_migration_progress</c> table if it doesn't exist.
    /// </summary>
    /// <remarks>
    /// <c>EnsureCreatedAsync()</c> is a no-op when the database already has tables
    /// (e.g., from host application migrations). We use <see cref="IRelationalDatabaseCreator"/>
    /// with a try/catch probe to detect missing tables.
    /// </remarks>
    private static async Task EnsureProgressTableAsync(MigrationProgressDbContext db, CancellationToken ct)
    {
        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.HasTablesAsync(ct).ConfigureAwait(false))
        {
            await creator.CreateTablesAsync(ct).ConfigureAwait(false);
            return;
        }

        // Database has tables (from host migrations) — EnsureCreated would be a no-op.
        // Try to query the progress table; if it fails, create it.
        try
        {
            await db.MigrationProgresses.AnyAsync(ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await creator.CreateTablesAsync(ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "No pending or in-progress migration cycles found at startup.")]
    private partial void LogNoPendingCycles();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dispatched {Count} migration batch command(s) at startup.")]
    private partial void LogCommandsDispatched(int count);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "An error occurred while resuming migration cycles at startup. Startup continues.")]
    private partial void LogStartupError(Exception ex);
}
