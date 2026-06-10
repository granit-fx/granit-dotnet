using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

#pragma warning disable GRSEC003 // Cache stores access tokens for the client credentials grant — token handling is inherent to its purpose

namespace Granit.Oidc.TokenManagement.Cache.Internal;

/// <summary>
/// FusionCache-backed token cache for client credentials tokens.
/// FusionCache prevents cache stampedes natively via its built-in factory locking.
/// </summary>
internal sealed partial class ClientCredentialsTokenCache(
    IFusionCache cache,
    ILogger<ClientCredentialsTokenCache> logger) : IClientCredentialsTokenCache
{
    private const string KeyPrefix = "oidc:cc:";

    /// <inheritdoc/>
    public async Task<string?> GetTokenAsync(string clientName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        MaybeValue<string?> maybe;
        try
        {
            maybe = await cache.TryGetAsync<string?>(BuildKey(clientName), token: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-open: a cache outage re-triggers a token endpoint round-trip
            // rather than failing the outbound request.
            LogCacheFailure(clientName, nameof(GetTokenAsync), ex.Message);
            return null;
        }

        if (!maybe.HasValue)
        {
            LogCacheMiss(clientName);
            return null;
        }

        LogCacheHit(clientName);
        return maybe.Value;
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

        try
        {
            await cache.SetAsync(
                BuildKey(clientName),
                accessToken,
                new FusionCacheEntryOptions { Duration = expiry },
                token: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheFailure(clientName, nameof(SetTokenAsync), ex.Message);
            return;
        }

        LogCacheSet(clientName, expiry);
    }

    /// <inheritdoc/>
    public async Task RemoveTokenAsync(string clientName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientName);

        try
        {
            await cache.RemoveAsync(BuildKey(clientName), token: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheFailure(clientName, nameof(RemoveTokenAsync), ex.Message);
            return;
        }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Client credentials token cache operation {Operation} failed for client {ClientName} — falling back to token endpoint. {ErrorMessage}")]
    private partial void LogCacheFailure(string clientName, string operation, string errorMessage);
}

#pragma warning restore GRSEC003
