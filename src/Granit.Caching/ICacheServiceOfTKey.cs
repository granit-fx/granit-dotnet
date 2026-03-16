using Microsoft.Extensions.Caching.Distributed;

namespace Granit.Caching;

/// <summary>
/// Typed cache service with a key of type <typeparamref name="TKey"/>.
/// The key is automatically converted to <see cref="string"/> via <c>key.ToString()</c>.
/// Extends <see cref="ICacheService{TCacheItem}"/> for compatibility with string keys.
/// </summary>
/// <typeparam name="TCacheItem">The type of the cached object.</typeparam>
/// <typeparam name="TKey">The key type. Must implement <c>ToString()</c> meaningfully.</typeparam>
/// <example>
/// Injection: <c>ICacheService&lt;UserCacheItem, Guid&gt; cache</c>
/// Usage: <c>await cache.GetOrAddAsync(userId, async cancellationToken =&gt; await repo.GetAsync(userId, cancellationToken));</c>
/// </example>
public interface ICacheService<TCacheItem, TKey> : ICacheService<TCacheItem>
    where TCacheItem : class
    where TKey : notnull
{
    /// <summary>
    /// Returns the cached item, or <c>null</c> if absent. The key is converted via <c>key.ToString()</c>.
    /// </summary>
    Task<TCacheItem?> GetAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// "Remember" pattern with a typed key. The key is converted via <c>key.ToString()</c>.
    /// </summary>
    Task<TCacheItem> GetOrAddAsync(
        TKey key,
        Func<CancellationToken, Task<TCacheItem>> factory,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an item with a typed key. The key is converted via <c>key.ToString()</c>.
    /// </summary>
    Task SetAsync(
        TKey key,
        TCacheItem value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the entry identified by the typed key.
    /// </summary>
    Task RemoveAsync(TKey key, CancellationToken cancellationToken = default);
}
