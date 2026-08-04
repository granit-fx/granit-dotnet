using Granit.Commands;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Hosted service that resumes pending and in-progress migration cycles at application startup.
/// </summary>
/// <remarks>
/// <para>
/// On startup, queries all <see cref="MigrationProgress"/> rows with status
/// <see cref="MigrationStatus.Pending"/> or <see cref="MigrationStatus.InProgress"/>
/// and dispatches one <see cref="RunMigrationBatchCommand"/> per cycle per tenant via
/// <see cref="ICommandSender"/>, resolved in a short-lived scope (<see cref="ICommandSender"/>
/// is Scoped so its <c>ICurrentTenant</c> / <c>ICurrentUserService</c> dependencies resolve
/// cleanly even though this service runs as a Singleton <see cref="IHostedService"/>).
/// </para>
/// <para>
/// If <see cref="ITenantEnumerator"/> yields tenant identifiers (Tenant-per-Schema or
/// Tenant-per-Database topologies), one command is dispatched per tenant per cycle,
/// using the stored cursor for resumption. Otherwise (single-tenant or Shared DB),
/// one command per progress row is dispatched, mapping a <c>null</c> tenant identifier to
/// <see cref="Guid.Empty"/>.
/// </para>
/// <para>
/// The whole resume pass runs under the <see cref="IGranitMigrationLock"/> (resource
/// <c>"GranitMigrationStartup"</c>): with N replicas starting concurrently, exactly one
/// dispatches the resume commands — the others skip. Without this, every replica would
/// dispatch the same commands from the same stored cursor, processing the same rows
/// N times concurrently.
/// </para>
/// <para>
/// Exceptions are caught and logged at <c>Error</c> level without blocking application startup.
/// </para>
/// </remarks>
internal sealed partial class MigrationStartupService(
    IDbContextFactory<MigrationProgressDbContext> progressFactory,
    ITenantEnumerator tenantEnumerator,
    IServiceScopeFactory scopeFactory,
    IGranitMigrationLock migrationLock,
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
        // Distributed lock: exactly one replica resumes pending cycles. The lock covers
        // command DISPATCH only (batch execution itself is guarded per-batch by the
        // executor's Completed check + the message pipeline) — but duplicated dispatch is
        // precisely the multi-replica hazard: N replicas building commands from the same
        // stored cursor.
        await using IAsyncDisposable? lockHandle = await migrationLock
            .TryAcquireAsync("GranitMigrationStartup", cancellationToken)
            .ConfigureAwait(false);

        if (lockHandle is null)
        {
            LogResumeSkipped();
            return;
        }

        // The data_migration_progress table is created by EF Core migrations
        // (via ConfigureMigrationsModule in the host DbContext).
        await using MigrationProgressDbContext db = await progressFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

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

        // ICommandSender is Scoped; create a short-lived scope so it (and its scoped
        // dependencies such as IMessageBus / OutgoingContextMiddleware) resolve correctly
        // from this Singleton IHostedService.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ICommandSender commandSender = scope.ServiceProvider.GetRequiredService<ICommandSender>();

        foreach (RunMigrationBatchCommand command in commands)
        {
            await commandSender.SendAsync(command, cancellationToken).ConfigureAwait(false);
        }

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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Migration cycle resume skipped — another instance holds the migration lock.")]
    private partial void LogResumeSkipped();

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
