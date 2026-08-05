using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Hosted service that triggers the resumption of pending and in-progress data-migration
/// cycles at application startup, by delegating to <see cref="IGranitMigrationRunner"/> in
/// <see cref="MigrationRunMode.ResumeBatches"/> mode.
/// </summary>
/// <remarks>
/// <para>
/// This service deliberately contains no orchestration of its own: the runner owns the
/// distributed lock, timeout, retry, and exit-code semantics for every migration path.
/// It historically ran a parallel pipeline with its own lock resource, which is exactly
/// the divergence class that let it ship without a lock at all (fixed in #3171,
/// structurally removed here — epic #3143 Phase 6).
/// </para>
/// <para>
/// The runner is optional at the DI level: it is registered by
/// <c>AddGranitMigrateSupport()</c> from the Hosting package. When absent, pending cycles
/// cannot resume — a warning is logged so the misconfiguration is visible instead of a
/// silent divergence.
/// </para>
/// <para>
/// Exceptions are caught and logged at <c>Error</c> level without blocking application startup.
/// </para>
/// </remarks>
internal sealed partial class MigrationStartupService(
    ILogger<MigrationStartupService> logger,
    IGranitMigrationRunner? migrationRunner = null) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (migrationRunner is null)
        {
            LogRunnerNotRegistered();
            return;
        }

        try
        {
            int exitCode = await migrationRunner
                .RunAsync(MigrationRunMode.ResumeBatches, cancellationToken)
                .ConfigureAwait(false);

            if (exitCode != 0)
            {
                LogResumeFailed(exitCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStartupError(ex);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No IGranitMigrationRunner is registered — pending migration cycles will NOT "
            + "resume at startup. Call AddGranitMigrateSupport() (Granit.Persistence."
            + "EntityFrameworkCore.Hosting) alongside AddGranitPersistenceMigrations().")]
    private partial void LogRunnerNotRegistered();

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Migration cycle resume finished with exit code {ExitCode}. Startup continues.")]
    private partial void LogResumeFailed(int exitCode);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "An error occurred while resuming migration cycles at startup. Startup continues.")]
    private partial void LogStartupError(Exception ex);
}
