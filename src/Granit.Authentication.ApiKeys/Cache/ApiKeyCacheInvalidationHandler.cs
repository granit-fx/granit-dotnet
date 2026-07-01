using Granit.Authentication.ApiKeys.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Authentication.ApiKeys.Cache;

/// <summary>
/// Wolverine message handler — evicts the stale <see cref="IApiKeyCacheService"/>
/// entry when an API key is revoked or its scopes change.
/// </summary>
/// <remarks>
/// <para>
/// Discovered by Wolverine's handler-scanning convention: the class name ends in
/// <c>"Handler"</c> and the method is named <c>HandleAsync</c> with the message as its
/// first parameter. Remaining parameters are resolved from the application's DI
/// container, so no <c>WolverineFx</c> package reference is required here (mirror of
/// <c>Granit.Authorization.Cache.PermissionCacheInvalidationHandler</c>).
/// </para>
/// <para>
/// Without this handler a revoked or leaked key keeps authenticating for up to
/// <c>ApiKeyAuthenticationOptions.CacheDuration</c> (default 5 minutes), because the
/// authentication handler caches the resolved <see cref="Domain.ApiKeyEntry"/> under its
/// hashed key. Both integration events carry the <c>HashedKey</c> precisely so the cache
/// can be evicted here — a hard <see cref="IApiKeyCacheService.RemoveAsync"/> (not a soft
/// expire) so stale-while-revalidate can never serve the revoked or over-scoped entry.
/// </para>
/// <para>
/// The cache is a soft dependency: when <c>Granit.Caching</c> is not installed, no
/// <see cref="IApiKeyCacheService"/> is registered and Wolverine simply cannot dispatch to
/// this handler — authentication then reads the store directly and revocation is immediate.
/// </para>
/// </remarks>
public partial class ApiKeyCacheInvalidationHandler
{
    /// <summary>
    /// Evicts the cached entry for a revoked API key so authentication fails immediately
    /// instead of after the cache TTL.
    /// </summary>
    public static async Task HandleAsync(
        ApiKeyRevokedEto evt,
        IApiKeyCacheService cacheService,
        ILogger<ApiKeyCacheInvalidationHandler> logger,
        CancellationToken cancellationToken)
    {
        await cacheService.RemoveAsync(evt.HashedKey, cancellationToken).ConfigureAwait(false);
        LogRevocationEviction(logger, evt.ApiKeyId);
    }

    /// <summary>
    /// Evicts the cached entry when an API key's scopes change so the new permissions take
    /// effect immediately instead of after the cache TTL.
    /// </summary>
    public static async Task HandleAsync(
        ApiKeyScopesUpdatedEto evt,
        IApiKeyCacheService cacheService,
        ILogger<ApiKeyCacheInvalidationHandler> logger,
        CancellationToken cancellationToken)
    {
        await cacheService.RemoveAsync(evt.HashedKey, cancellationToken).ConfigureAwait(false);
        LogScopeEviction(logger, evt.ApiKeyId);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Evicted cached API key {ApiKeyId} after revocation.")]
    private static partial void LogRevocationEviction(ILogger logger, Guid apiKeyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Evicted cached API key {ApiKeyId} after scope update.")]
    private static partial void LogScopeEviction(ILogger logger, Guid apiKeyId);
}
