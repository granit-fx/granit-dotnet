using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.RateLimiting.Internal;

/// <summary>
/// Fails the host at startup when rate limiting would run on the per-process counter
/// store outside Development — per-pod counters multiply every limit by the replica
/// count, so a "100 req/s" policy silently becomes N×100 cluster-wide.
/// Always logs which counter store is active so operators can verify the wiring.
/// </summary>
internal sealed partial class RateLimitingStartupGuard(
    IServiceProvider serviceProvider,
    IOptions<GranitRateLimitingOptions> options,
    IHostEnvironment environment,
    ILogger<RateLimitingStartupGuard> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        GranitRateLimitingOptions opts = options.Value;
        if (!opts.Enabled)
        {
            LogDisabled(logger);
            return Task.CompletedTask;
        }

        using IServiceScope scope = serviceProvider.CreateScope();
        IRateLimitCounterStore store = scope.ServiceProvider.GetRequiredService<IRateLimitCounterStore>();

        if (store is not InMemoryRateLimitCounterStore)
        {
            LogDistributedStore(logger, store.GetType().Name);
            return Task.CompletedTask;
        }

        if (environment.IsDevelopment() || opts.AllowInMemoryCounterStore)
        {
            LogInMemoryStore(logger, environment.EnvironmentName);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException(
            "Granit rate limiting is backed by the per-process in-memory counter store in the " +
            $"'{environment.EnvironmentName}' environment. Under multiple replicas every limit is " +
            "multiplied by the replica count. Register a Redis IConnectionMultiplexer for " +
            "cluster-wide counters or, for a genuinely single-instance deployment, set " +
            $"'{GranitRateLimitingOptions.SectionName}:{nameof(GranitRateLimitingOptions.AllowInMemoryCounterStore)}' to true.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rate limiting is disabled — counter-store guard skipped.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rate limiting counter store: {StoreType} (distributed).")]
    private static partial void LogDistributedStore(ILogger logger, string storeType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rate limiting counter store: InMemoryRateLimitCounterStore (NOT distributed) in environment {Environment}. " +
                  "Limits are enforced per replica, not cluster-wide.")]
    private static partial void LogInMemoryStore(ILogger logger, string environment);
}
