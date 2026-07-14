using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Models;

namespace Granit.Http.Idempotency.Internal;

/// <summary>
/// Per-process <see cref="IIdempotencyStore"/> — the Development default. All transitions
/// are atomic under a single <see cref="Lock"/>; TTLs are evaluated against
/// <see cref="TimeProvider"/> so tests can advance time deterministically.
/// </summary>
/// <remarks>
/// <para>
/// Keys arrive fully tenant/user-namespaced from the middleware — this store performs no
/// re-namespacing (see <see cref="IIdempotencyStore"/> remarks). Entries are live object
/// graphs, never serialized, so at-rest encryption does not apply here — same threat model
/// as <c>IMemoryCache</c>.
/// </para>
/// <para>
/// NOT distributed: two replicas each hold their own dictionary, so the same
/// <c>Idempotency-Key</c> routed to two pods executes twice. <see cref="IdempotencyStartupGuard"/>
/// fails the host outside Development unless <see cref="IdempotencyOptions.AllowInMemoryStore"/>
/// is explicitly set.
/// </para>
/// </remarks>
internal sealed class InMemoryIdempotencyStore(TimeProvider timeProvider) : IIdempotencyStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, CacheSlot> _entries = [];

    private sealed record CacheSlot(IdempotencyEntry Entry, DateTimeOffset ExpiresAt);

    /// <inheritdoc/>
    public bool IsDistributed => false;

    /// <inheritdoc/>
    public string BackendName => nameof(InMemoryIdempotencyStore);

    /// <inheritdoc/>
    public Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            EvictExpired(now);

            if (_entries.ContainsKey(key))
            {
                return Task.FromResult(false);
            }

            _entries[key] = new CacheSlot(entry, now + ttl);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out CacheSlot? slot))
            {
                return Task.FromResult<IdempotencyEntry?>(null);
            }

            if (slot.ExpiresAt <= now)
            {
                _entries.Remove(key);
                return Task.FromResult<IdempotencyEntry?>(null);
            }

            return Task.FromResult<IdempotencyEntry?>(slot.Entry);
        }
    }

    /// <inheritdoc/>
    public Task<bool> CompleteAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken) =>
        SetIfPresent(key, entry, ttl);

    /// <inheritdoc/>
    public Task<bool> TombstoneAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken) =>
        SetIfPresent(key, entry, ttl);

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    // Complete and Tombstone share SET XX semantics: overwrite only while the key exists,
    // never create it (see IIdempotencyStore remarks on why existence is a sufficient guard).
    private Task<bool> SetIfPresent(string key, IdempotencyEntry entry, TimeSpan ttl)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out CacheSlot? slot))
            {
                return Task.FromResult(false);
            }

            if (slot.ExpiresAt <= now)
            {
                _entries.Remove(key);
                return Task.FromResult(false);
            }

            _entries[key] = new CacheSlot(entry, now + ttl);
            return Task.FromResult(true);
        }
    }

    // Opportunistic sweep on the write path — mirrors Redis lazy expiry closely enough for a
    // Development store and bounds memory without a background timer. Caller holds _lock.
    private void EvictExpired(DateTimeOffset now)
    {
        List<string>? expired = null;
        foreach ((string key, CacheSlot slot) in _entries)
        {
            if (slot.ExpiresAt <= now)
            {
                (expired ??= []).Add(key);
            }
        }

        if (expired is not null)
        {
            foreach (string key in expired)
            {
                _entries.Remove(key);
            }
        }
    }
}
