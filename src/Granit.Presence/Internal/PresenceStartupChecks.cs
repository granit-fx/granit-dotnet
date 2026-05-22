using Granit.Caching;
using Granit.Presence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Presence.Internal;

/// <summary>
/// Emits one-shot startup warnings for known risky configurations:
/// running with the in-memory store in Production, or running with an
/// unencrypted cache value encryptor.
/// </summary>
internal sealed partial class PresenceStartupChecks(
    IServiceProvider services,
    IHostEnvironment environment,
    ILogger<PresenceStartupChecks> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsProduction())
        {
            return Task.CompletedTask;
        }

        IPresenceStore store = services.GetRequiredService<IPresenceStore>();
        if (store is InMemoryPresenceStore)
        {
            LogInMemoryStoreInProduction(logger);
        }

        ICacheValueEncryptor? encryptor = services.GetService<ICacheValueEncryptor>();
        if (encryptor is null || encryptor is NullCacheValueEncryptor)
        {
            LogUnencryptedCache(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Warning,
        Message = "Granit.Presence is using the in-memory presence store in a Production environment. Overrides will be lost on restart and will not propagate across pods. Register AddGranitPresenceEntityFrameworkCore for durable persistence.")]
    private static partial void LogInMemoryStoreInProduction(ILogger logger);

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Warning,
        Message = "Granit.Presence stores user activity heartbeats in the cache backplane without value encryption (ICacheValueEncryptor is null or no-op). For multi-tenant or PII-sensitive deployments, configure AesCacheValueEncryptor via CacheEncryptionOptions.")]
    private static partial void LogUnencryptedCache(ILogger logger);
}
