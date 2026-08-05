using Granit.Commands;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Default <see cref="IMigrationBatchResumer"/>: queries all <see cref="MigrationProgress"/>
/// rows with status <see cref="MigrationStatus.Pending"/> or <see cref="MigrationStatus.InProgress"/>
/// and dispatches one <see cref="RunMigrationBatchCommand"/> per cycle per tenant via
/// <see cref="ICommandSender"/>, resolved in a short-lived scope (<see cref="ICommandSender"/>
/// is Scoped so its <c>ICurrentTenant</c> / <c>ICurrentUserService</c> dependencies resolve
/// cleanly even from a Singleton caller).
/// </summary>
/// <remarks>
/// <para>
/// If <see cref="ITenantEnumerator"/> yields tenant identifiers (Tenant-per-Schema or
/// Tenant-per-Database topologies), one command is dispatched per tenant per cycle,
/// using the stored cursor for resumption. Otherwise (single-tenant or Shared DB),
/// one command per progress row is dispatched, mapping a <c>null</c> tenant identifier to
/// <see cref="Guid.Empty"/>.
/// </para>
/// <para>
/// No locking here: exactly-one-replica semantics are enforced by the caller,
/// <see cref="IGranitMigrationRunner"/>, under its <c>"GranitMigration"</c> resource.
/// </para>
/// </remarks>
internal sealed partial class MigrationBatchResumer(
    IDbContextFactory<MigrationProgressDbContext> progressFactory,
    ITenantEnumerator tenantEnumerator,
    IServiceScopeFactory scopeFactory,
    IOptions<MigrationStartupOptions> options,
    ILogger<MigrationBatchResumer> logger) : IMigrationBatchResumer
{
    /// <inheritdoc/>
    public async Task<int> ResumeAsync(CancellationToken cancellationToken)
    {
        // The data_migration_progress table is created by EF Core migrations
        // (via ConfigureMigrationsModule in the host DbContext).
        await using MigrationProgressDbContext db = await progressFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<MigrationProgress> pending = await db.MigrationProgresses
            .Where(p => p.Status == MigrationStatus.Pending || p.Status == MigrationStatus.InProgress)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            LogNoPendingCycles();
            return 0;
        }

        List<Guid> tenantIds = [];
        await foreach (Guid tenantId in tenantEnumerator.GetActiveTenantIdsAsync(cancellationToken).ConfigureAwait(false))
        {
            tenantIds.Add(tenantId);
        }

        int batchSize = options.Value.DefaultBatchSize;
        List<RunMigrationBatchCommand> commands = BuildCommands(pending, tenantIds, batchSize);

        // ICommandSender is Scoped; create a short-lived scope so it (and its scoped
        // dependencies such as IMessageBus / OutgoingContextMiddleware) resolve correctly.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ICommandSender commandSender = scope.ServiceProvider.GetRequiredService<ICommandSender>();

        foreach (RunMigrationBatchCommand command in commands)
        {
            await commandSender.SendAsync(command, cancellationToken).ConfigureAwait(false);
        }

        LogCommandsDispatched(commands.Count);
        return commands.Count;
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

    [LoggerMessage(Level = LogLevel.Information,
        Message = "No pending or in-progress migration cycles found.")]
    private partial void LogNoPendingCycles();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dispatched {Count} migration batch command(s) to resume pending cycles.")]
    private partial void LogCommandsDispatched(int count);
}
