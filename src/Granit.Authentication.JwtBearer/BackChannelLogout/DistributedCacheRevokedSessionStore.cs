using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authentication.JwtBearer.BackChannelLogout;

/// <summary>
/// <see cref="IRevokedSessionStore"/> backed by <see cref="IFusionCache"/>.
/// Each revoked session is stored as a simple existence marker with a TTL.
/// </summary>
internal sealed partial class DistributedCacheRevokedSessionStore(
    IFusionCache cache,
    ILogger<DistributedCacheRevokedSessionStore> logger) : IRevokedSessionStore
{
    private const string KeyPrefix = "granit:revoked-session:";

    /// <inheritdoc/>
    public async Task RevokeSessionAsync(string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        string key = KeyPrefix + sessionId;

        await cache.SetAsync(key, true, new FusionCacheEntryOptions { Duration = ttl }, token: cancellationToken).ConfigureAwait(false);
        LogSessionRevoked(logger, sessionId, ttl);
    }

    /// <inheritdoc/>
    public async Task<bool> IsSessionRevokedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        string key = KeyPrefix + sessionId;
        MaybeValue<bool> maybe = await cache.TryGetAsync<bool>(key, token: cancellationToken).ConfigureAwait(false);
        return maybe.HasValue;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session '{SessionId}' revoked via back-channel logout (TTL: {Ttl}).")]
    private static partial void LogSessionRevoked(ILogger logger, string sessionId, TimeSpan ttl);
}
