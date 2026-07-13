using Granit.Caching;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Logging;

namespace Granit.Http.Idempotency.Internal;

/// <summary>
/// <see cref="IConditionalCache"/>-backed implementation of <see cref="IIdempotencyStore"/>.
/// Delegates atomic SET NX / SET XX semantics to the underlying cache (in-memory or Redis).
/// </summary>
internal sealed partial class ConditionalCacheIdempotencyStore(
    IConditionalCache cache,
    ILogger<ConditionalCacheIdempotencyStore> logger) : IIdempotencyStore
{
    /// <summary>Whether the underlying cache is shared across replicas — see <see cref="IConditionalCache.IsDistributed"/>.</summary>
    internal bool IsDistributed => cache.IsDistributed;

    /// <summary>Backend implementation name, for the startup log.</summary>
    internal string BackendName => cache.GetType().Name;

    /// <inheritdoc/>
    public Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken) =>
        cache.SetIfAbsentAsync(key, entry, ttl, cancellationToken);

    /// <inheritdoc/>
    public Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken) =>
        cache.GetAsync<IdempotencyEntry>(key, cancellationToken);

    /// <inheritdoc/>
    public async Task SetCompletedAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        bool updated = await cache.SetIfPresentAsync(key, entry, ttl, cancellationToken).ConfigureAwait(false);

        if (!updated)
        {
            LogKeyExpiredBeforeCompletion(key);
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        cache.DeleteAsync(key, cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SetCompletedAsync: key {Key} no longer exists (InProgress TTL may have expired before response completed).")]
    private partial void LogKeyExpiredBeforeCompletion(string key);
}
