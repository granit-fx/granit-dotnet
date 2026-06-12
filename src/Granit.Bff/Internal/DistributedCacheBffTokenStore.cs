using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Internal;

/// <summary>
/// <see cref="IBffTokenStore"/> implementation backed by <see cref="IFusionCache"/>.
/// Keys follow the pattern <c>bff:session:{frontendName}:{sessionId}</c>.
/// </summary>
internal sealed class DistributedCacheBffTokenStore(
    IFusionCache cache,
    IOptions<GranitBffOptions> options,
    IClock clock) : IBffTokenStore
{
    private const string KeyPrefix = "bff:session:";
    private const string UserIndexPrefix = "bff:user-sessions:";

    public async Task StoreAsync(string frontendName, string sessionId, BffTokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(tokens);

        // Stamp the absolute expiry so a later TouchAsync can renew the entry without extending the session
        // (mirrors the EF Core store's ExpiresAt column). A full re-store is the only path that moves it.
        TimeSpan sessionDuration = options.Value.SessionDuration;
        BffTokenSet stored = tokens with { SessionExpiresAt = clock.Now.Add(sessionDuration) };
        FusionCacheEntryOptions cacheOptions = new() { Duration = sessionDuration };

        await cache.SetAsync(BuildKey(frontendName, sessionId), stored, cacheOptions, token: cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(tokens.UserId))
        {
            await AddToUserIndexAsync(frontendName, tokens.UserId, sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<BffTokenSet?> GetAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        MaybeValue<BffTokenSet> maybe = await cache.TryGetAsync<BffTokenSet>(BuildKey(frontendName, sessionId), token: cancellationToken)
            .ConfigureAwait(false);

        return maybe.HasValue ? maybe.Value : null;
    }

    public async Task TouchAsync(
        string frontendName, string sessionId, DateTimeOffset lastAccessedAt, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Read-modify-write of the cached entry: not atomic, but the updated fields are display-only
        // (last-activity, IP) and the caller throttles touches, so a lost race only drops one activity sample.
        BffTokenSet? tokens = await GetAsync(frontendName, sessionId, cancellationToken).ConfigureAwait(false);
        if (tokens is null)
        {
            return;
        }

        // Renew the entry with fresh activity fields while PRESERVING the original expiry: re-store with the TTL
        // remaining until SessionExpiresAt, never a full SessionDuration. A cheap touch must not extend the
        // session — only a full StoreAsync (login, refresh, sliding extension) does — so this matches the EF Core
        // store (which leaves ExpiresAt untouched on touch) and respects SessionAbsoluteMaxDuration.
        DateTimeOffset expiresAt = tokens.SessionExpiresAt ?? clock.Now.Add(options.Value.SessionDuration);
        TimeSpan remaining = expiresAt - clock.Now;
        if (remaining <= TimeSpan.Zero)
        {
            // Already at/past expiry — let the entry lapse rather than resurrecting it with a new TTL.
            return;
        }

        BffTokenSet updated = tokens with { LastAccessedAt = lastAccessedAt, IpAddress = ipAddress };
        FusionCacheEntryOptions cacheOptions = new() { Duration = remaining };

        await cache.SetAsync(BuildKey(frontendName, sessionId), updated, cacheOptions, token: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        BffTokenSet? tokens = await GetAsync(frontendName, sessionId, cancellationToken).ConfigureAwait(false);
        if (tokens?.UserId is not null)
        {
            await RemoveFromUserIndexAsync(frontendName, tokens.UserId, sessionId, cancellationToken)
                .ConfigureAwait(false);
        }

        await cache.RemoveAsync(BuildKey(frontendName, sessionId), token: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetSessionIdsByUserAsync(
        string frontendName, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        MaybeValue<List<string>> maybe = await cache.TryGetAsync<List<string>>(BuildUserIndexKey(frontendName, userId), token: cancellationToken)
            .ConfigureAwait(false);

        return maybe.HasValue ? maybe.Value ?? [] : [];
    }

    // NOTE: AddToUserIndexAsync/RemoveFromUserIndexAsync use a non-atomic read-modify-write
    // pattern. Two concurrent logins from the same user can race, causing one session to be
    // omitted from the index (it remains active but invisible to session management).
    // Accepted risk: the window is narrow, the EF Core store is not affected, and the
    // cleanup job eventually removes orphaned sessions. A proper fix requires Redis SADD
    // or distributed locking which is beyond the scope of the FusionCache abstraction.
    private async Task AddToUserIndexAsync(
        string frontendName, string userId, string sessionId, CancellationToken cancellationToken)
    {
        string indexKey = BuildUserIndexKey(frontendName, userId);
        List<string> sessionIds = await GetUserIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

        if (!sessionIds.Contains(sessionId))
        {
            sessionIds.Add(sessionId);
            await SaveUserIndexAsync(indexKey, sessionIds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RemoveFromUserIndexAsync(
        string frontendName, string userId, string sessionId, CancellationToken cancellationToken)
    {
        string indexKey = BuildUserIndexKey(frontendName, userId);
        List<string> sessionIds = await GetUserIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

        if (sessionIds.Remove(sessionId))
        {
            await SaveUserIndexAsync(indexKey, sessionIds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<string>> GetUserIndexAsync(string indexKey, CancellationToken cancellationToken)
    {
        MaybeValue<List<string>> maybe = await cache.TryGetAsync<List<string>>(indexKey, token: cancellationToken).ConfigureAwait(false);
        return maybe.HasValue ? maybe.Value ?? [] : [];
    }

    private async Task SaveUserIndexAsync(string indexKey, List<string> sessionIds, CancellationToken cancellationToken)
    {
        FusionCacheEntryOptions cacheOptions = new() { Duration = options.Value.SessionAbsoluteMaxDuration };

        await cache.SetAsync(indexKey, sessionIds, cacheOptions, token: cancellationToken).ConfigureAwait(false);
    }

    private static string BuildKey(string frontendName, string sessionId) => $"{KeyPrefix}{frontendName}:{sessionId}";
    private static string BuildUserIndexKey(string frontendName, string userId) => $"{UserIndexPrefix}{frontendName}:{userId}";
}
