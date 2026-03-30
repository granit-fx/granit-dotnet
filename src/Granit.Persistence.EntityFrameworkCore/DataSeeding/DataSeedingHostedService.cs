using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Hosted service that triggers data seeding at application startup.
/// </summary>
/// <remarks>
/// Calls <see cref="IDataSeeder.SeedAsync"/> once during <see cref="IHostedService.StartAsync"/>
/// with a host-level <see cref="DataSeedContext"/> (<see cref="DataSeedContext.TenantId"/> = <c>null</c>).
/// Exceptions are caught and logged — seeding failures never block application startup.
/// </remarks>
internal sealed partial class DataSeedingHostedService(
    IDataSeeder seeder,
    ILogger<DataSeedingHostedService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
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

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Error,
        Message = "An error occurred during data seeding at startup. Startup continues.")]
    private partial void LogSeedingError(Exception ex);
}
