using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Hosted service that triggers data seeding at application startup.
/// </summary>
/// <remarks>
/// <para>
/// Calls <see cref="IDataSeeder.SeedAsync"/> once during
/// <see cref="IHostedLifecycleService.StartedAsync"/> with a host-level
/// <see cref="DataSeedContext"/> (<see cref="DataSeedContext.TenantId"/> = <c>null</c>).
/// Exceptions are caught and logged — seeding failures never block application startup.
/// </para>
/// <para>
/// Seeding runs in <c>StartedAsync</c> (not <c>StartAsync</c>) to ensure all hosted
/// services — including messaging infrastructure like Wolverine — have fully started
/// before seed contributors execute. This prevents <c>WolverineHasNotStartedException</c>
/// when seed contributors use services that publish events.
/// </para>
/// </remarks>
internal sealed partial class DataSeedingHostedService(
    IDataSeeder seeder,
    ILogger<DataSeedingHostedService> logger) : IHostedLifecycleService
{
    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedLifecycleService.StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedLifecycleService.StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IHostedLifecycleService.StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Executes data seeding after all hosted services have started.
    /// </summary>
    async Task IHostedLifecycleService.StartedAsync(CancellationToken cancellationToken)
    {
        try
        {
            DataSeedContext context = new();
            await seeder.SeedAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSeedingError(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "An error occurred during data seeding at startup. Startup continues.")]
    private partial void LogSeedingError(Exception ex);
}
