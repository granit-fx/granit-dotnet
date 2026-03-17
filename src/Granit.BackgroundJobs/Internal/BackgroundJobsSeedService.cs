using Granit.BackgroundJobs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Hosted service that seeds the <see cref="IBackgroundJobStoreWriter"/> with all jobs
/// discovered at startup via <see cref="RecurringJobDiscovery"/>.
/// Runs once on application start, before any Wolverine handlers are invoked.
/// </summary>
internal sealed class BackgroundJobsSeedService(
    IServiceScopeFactory scopeFactory,
    IReadOnlyList<RecurringJobRegistration> registrations) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // IBackgroundJobStoreWriter is Scoped when using the EF Core provider.
        // IHostedService runs as a singleton-like root-scope service; create an explicit
        // scope so that Scoped dependencies are resolved correctly.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IBackgroundJobStoreWriter storeWriter =
            scope.ServiceProvider.GetRequiredService<IBackgroundJobStoreWriter>();
        await storeWriter.SeedJobsAsync(registrations, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
