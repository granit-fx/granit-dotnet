using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Idempotency.Internal;

/// <summary>
/// Fails the host at startup when idempotency would run on a non-distributed store
/// outside Development — a per-pod store silently breaks the at-most-once guarantee
/// under multiple replicas (the same key routed to two pods executes twice).
/// Always logs which backend is active so operators can verify the wiring.
/// </summary>
internal sealed partial class IdempotencyStartupGuard(
    IServiceProvider serviceProvider,
    IOptions<IdempotencyOptions> options,
    IHostEnvironment environment,
    ILogger<IdempotencyStartupGuard> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        Abstractions.IIdempotencyStore store = scope.ServiceProvider.GetRequiredService<Abstractions.IIdempotencyStore>();

        if (store is not ConditionalCacheIdempotencyStore conditionalStore)
        {
            // Custom store — the host made a deliberate choice; log it and trust it.
            LogCustomStore(logger, store.GetType().Name);
            return Task.CompletedTask;
        }

        if (conditionalStore.IsDistributed)
        {
            LogDistributedStore(logger, conditionalStore.BackendName);
            return Task.CompletedTask;
        }

        if (environment.IsDevelopment() || options.Value.AllowInMemoryStore)
        {
            LogInMemoryStore(logger, conditionalStore.BackendName, environment.EnvironmentName);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException(
            $"Granit idempotency is backed by the non-distributed '{conditionalStore.BackendName}' in the " +
            $"'{environment.EnvironmentName}' environment. Under multiple replicas the same Idempotency-Key " +
            "can execute twice (double payment, double email). Register a distributed IConditionalCache " +
            "(Granit.Caching.StackExchangeRedis) or, for a genuinely single-instance deployment, set " +
            $"'{IdempotencyOptions.SectionName}:{nameof(IdempotencyOptions.AllowInMemoryStore)}' to true.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Idempotency store: custom implementation {StoreType} — distributed-store guard skipped.")]
    private static partial void LogCustomStore(ILogger logger, string storeType);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Idempotency store: {Backend} (distributed).")]
    private static partial void LogDistributedStore(ILogger logger, string backend);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Idempotency store: {Backend} (NOT distributed) in environment {Environment}. " +
                  "The at-most-once guarantee only holds for a single replica.")]
    private static partial void LogInMemoryStore(ILogger logger, string backend, string environment);
}
