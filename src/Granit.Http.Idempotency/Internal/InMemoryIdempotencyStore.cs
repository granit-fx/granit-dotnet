using System.Collections.Concurrent;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Models;

namespace Granit.Http.Idempotency.Internal;

/// <summary>
/// In-memory fallback implementation of <see cref="IIdempotencyStore"/> used when
/// no <c>IConnectionMultiplexer</c> (Redis) is registered in the container.
/// </summary>
/// <remarks>
/// This store is suitable for development and single-instance deployments only.
/// It does not survive process restarts and does not provide cross-instance deduplication.
/// TTL expiration is checked lazily on read; a background cleanup is not implemented.
/// </remarks>
internal sealed class InMemoryIdempotencyStore(TimeProvider timeProvider) : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (IdempotencyEntry Entry, DateTimeOffset ExpiresAt)> _store = new();

    /// <inheritdoc/>
    public Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        Cleanup();
        DateTimeOffset expiresAt = timeProvider.GetUtcNow() + ttl;
        bool acquired = _store.TryAdd(key, (entry, expiresAt));
        return Task.FromResult(acquired);
    }

    /// <inheritdoc/>
    public Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(key, out (IdempotencyEntry Entry, DateTimeOffset ExpiresAt) tuple))
        {
            if (timeProvider.GetUtcNow() < tuple.ExpiresAt)
            {
                return Task.FromResult<IdempotencyEntry?>(tuple.Entry);
            }

            _store.TryRemove(key, out _);
        }

        return Task.FromResult<IdempotencyEntry?>(null);
    }

    /// <inheritdoc/>
    public Task SetCompletedAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        DateTimeOffset expiresAt = timeProvider.GetUtcNow() + ttl;
        _store.AddOrUpdate(key, (entry, expiresAt), (_, _) => (entry, expiresAt));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (KeyValuePair<string, (IdempotencyEntry Entry, DateTimeOffset ExpiresAt)> kvp in _store)
        {
            if (now >= kvp.Value.ExpiresAt)
            {
                _store.TryRemove(kvp.Key, out _);
            }
        }
    }
}
