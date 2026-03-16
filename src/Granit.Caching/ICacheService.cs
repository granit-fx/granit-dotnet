using Granit.Caching.Options;
using Microsoft.Extensions.Caching.Distributed;

namespace Granit.Caching;

/// <summary>
/// Typed cache service built on top of <see cref="IDistributedCache"/>.
/// Handles JSON serialization automatically, generates prefixed keys
/// (<c>{KeyPrefix}:{CacheName}:{userKey}</c>), and provides stampede protection via double-check locking.
/// </summary>
/// <typeparam name="TCacheItem">The type of the cached object. Must be a class.</typeparam>
/// <example>
/// Injection: <c>ICacheService&lt;UserCacheItem&gt; cache</c>
/// Usage: <c>await cache.GetOrAddAsync(userId.ToString(), async cancellationToken =&gt; await repo.GetAsync(userId, cancellationToken));</c>
/// </example>
public interface ICacheService<TCacheItem> where TCacheItem : class
{
    /// <summary>
    /// Returns the cached item, or <c>null</c> if it does not exist or has expired.
    /// </summary>
    /// <param name="key">User key (without prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TCacheItem?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cached item or executes <paramref name="factory"/> exactly once under concurrency
    /// (stampede protection via double-check locking).
    /// Inspired by Laravel's <c>remember()</c> pattern.
    /// </summary>
    /// <param name="key">User key.</param>
    /// <param name="factory">Factory executed when the item is absent from the cache.</param>
    /// <param name="options">TTL options for this entry. When <c>null</c>, uses the defaults from <see cref="CachingOptions"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TCacheItem> GetOrAddAsync(
        string key,
        Func<CancellationToken, Task<TCacheItem>> factory,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an item in the cache with the specified options.
    /// </summary>
    /// <param name="key">User key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="options">TTL options. When <c>null</c>, uses the defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(
        string key,
        TCacheItem value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the cache entry identified by the given key.
    /// </summary>
    /// <param name="key">User key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the sliding expiration of an entry without changing its value.
    /// </summary>
    /// <param name="key">User key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshAsync(string key, CancellationToken cancellationToken = default);
}
