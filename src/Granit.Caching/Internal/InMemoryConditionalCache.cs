using System.Collections.Concurrent;

namespace Granit.Caching.Internal;

/// <summary>
/// In-memory implementation of <see cref="IConditionalCache"/> using <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for development and single-instance deployments only.
/// </summary>
/// <remarks>
/// Data does not survive process restarts. TTL expiration is checked lazily on read
/// and periodically cleaned up during <see cref="SetIfAbsentAsync{T}"/> calls.
/// Keys are namespaced by <see cref="ConditionalCacheKeyComposer"/> (app prefix + tenant
/// segment) so tenants sharing a process cannot observe each other's entries.
/// </remarks>
internal sealed class InMemoryConditionalCache(
    TimeProvider timeProvider,
    ConditionalCacheKeyComposer keyComposer) : IConditionalCache
{
    private readonly ConcurrentDictionary<string, (object? Value, DateTimeOffset ExpiresAt)> _store = new();
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public Task<bool> SetIfAbsentAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        key = keyComposer.Compose(key);

        lock (_lock)
        {
            Cleanup();
            DateTimeOffset expiresAt = timeProvider.GetUtcNow() + ttl;

            if (_store.TryGetValue(key, out (object? Value, DateTimeOffset ExpiresAt) existing) && timeProvider.GetUtcNow() >= existing.ExpiresAt)
            {
                _store.TryRemove(key, out _);
            }

            bool added = _store.TryAdd(key, (value, expiresAt));
            return Task.FromResult(added);
        }
    }

    /// <inheritdoc/>
    public Task<bool> SetIfPresentAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        key = keyComposer.Compose(key);

        lock (_lock)
        {
            if (_store.TryGetValue(key, out (object? Value, DateTimeOffset ExpiresAt) existing) && timeProvider.GetUtcNow() < existing.ExpiresAt)
            {
                _store[key] = (value, timeProvider.GetUtcNow() + ttl);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        key = keyComposer.Compose(key);

        if (_store.TryGetValue(key, out (object? Value, DateTimeOffset ExpiresAt) tuple))
        {
            if (timeProvider.GetUtcNow() < tuple.ExpiresAt)
            {
                return Task.FromResult((T?)tuple.Value);
            }

            _store.TryRemove(key, out _);
        }

        return Task.FromResult(default(T?));
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _store.TryRemove(keyComposer.Compose(key), out _);
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (string key in _store.Where(kvp => now >= kvp.Value.ExpiresAt).Select(kvp => kvp.Key))
        {
            _store.TryRemove(key, out _);
        }
    }
}
