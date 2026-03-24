using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

#pragma warning disable GRSEC003 // Cache stores access tokens for the client credentials grant — token handling is inherent to its purpose

namespace Granit.Oidc.TokenManagement.Cache.Internal;

/// <summary>
/// Distributed-cache-backed token cache for client credentials tokens.
/// Uses a <see cref="SemaphoreSlim"/> per client name to prevent cache stampedes
/// when multiple requests race to refresh an expired token.
/// </summary>
internal sealed partial class ClientCredentialsTokenCache(
    IDistributedCache distributedCache,
    ILogger<ClientCredentialsTokenCache> logger) : IClientCredentialsTokenCache
{
    private const string KeyPrefix = "oidc:cc:";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task<string?> GetTokenAsync(string clientName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        byte[]? bytes = await distributedCache
            .GetAsync(BuildKey(clientName), cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null)
        {
            LogCacheMiss(clientName);
            return null;
        }

        LogCacheHit(clientName);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <inheritdoc/>
    public async Task SetTokenAsync(
        string clientName,
        string accessToken,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        SemaphoreSlim semaphore = _locks.GetOrAdd(clientName, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry,
            };

            byte[] bytes = Encoding.UTF8.GetBytes(accessToken);
            await distributedCache
                .SetAsync(BuildKey(clientName), bytes, options, cancellationToken)
                .ConfigureAwait(false);

            LogCacheSet(clientName, expiry);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RemoveTokenAsync(string clientName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        await distributedCache
            .RemoveAsync(BuildKey(clientName), cancellationToken)
            .ConfigureAwait(false);

        LogCacheRemoved(clientName);
    }

    private static string BuildKey(string clientName) => $"{KeyPrefix}{clientName}";

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client credentials token cache hit for client {ClientName}")]
    private partial void LogCacheHit(string clientName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client credentials token cache miss for client {ClientName}")]
    private partial void LogCacheMiss(string clientName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached client credentials token for client {ClientName} with expiry {Expiry}")]
    private partial void LogCacheSet(string clientName, TimeSpan expiry);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed cached client credentials token for client {ClientName}")]
    private partial void LogCacheRemoved(string clientName);
}

#pragma warning restore GRSEC003
