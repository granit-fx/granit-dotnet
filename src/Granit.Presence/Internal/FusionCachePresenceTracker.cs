using Granit.Presence.Abstractions;
using Granit.Presence.Domain;
using Granit.Presence.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Presence.Internal;

/// <summary>
/// <see cref="IPresenceTracker"/> backed by <see cref="IFusionCache"/>. Uses an L1 in-memory
/// cache (provided by <c>Granit.Caching</c>) and a Redis L2 backplane when
/// <c>Granit.Caching.StackExchangeRedis</c> is loaded — so heartbeats are visible across pods.
/// </summary>
internal sealed class FusionCachePresenceTracker(
    IFusionCache cache,
    IClock clock,
    IOptionsMonitor<PresenceOptions> options) : IPresenceTracker
{
    private const string CacheKeyPrefix = "granit:presence:";

    public async Task<PresenceHeartbeat> RecordPollAsync(
        Guid userId,
        TimeSpan idleDuration,
        CancellationToken cancellationToken)
    {
        if (idleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(idleDuration),
                idleDuration,
                "Idle duration cannot be negative.");
        }

        PresenceOptions opts = options.CurrentValue;
        TimeSpan clampUpper = opts.OfflineThreshold + opts.OfflineThreshold;
        if (idleDuration > clampUpper)
        {
            idleDuration = clampUpper;
        }

        DateTimeOffset nowUtc = clock.Now;
        DateTimeOffset computedActivity = nowUtc - idleDuration;
        string key = CacheKey(userId);

        PresenceHeartbeat? existing = await cache.GetOrDefaultAsync<PresenceHeartbeat?>(
            key, defaultValue: null, token: cancellationToken).ConfigureAwait(false);

        DateTimeOffset mergedActivity = existing is not null && existing.LastActivityUtc > computedActivity
            ? existing.LastActivityUtc
            : computedActivity;

        PresenceHeartbeat merged = new(nowUtc, mergedActivity);

        await cache.SetAsync(
            key,
            merged,
            opts.HeartbeatCacheTtl,
            token: cancellationToken).ConfigureAwait(false);

        return merged;
    }

    public async Task<PresenceHeartbeat?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await cache.GetOrDefaultAsync<PresenceHeartbeat?>(
            CacheKey(userId), defaultValue: null, token: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, PresenceHeartbeat>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        Dictionary<Guid, PresenceHeartbeat> result = [];
        foreach (Guid userId in userIds)
        {
            PresenceHeartbeat? hb = await cache.GetOrDefaultAsync<PresenceHeartbeat?>(
                CacheKey(userId), defaultValue: null, token: cancellationToken).ConfigureAwait(false);

            if (hb is not null)
            {
                result[userId] = hb;
            }
        }

        return result;
    }

    public async Task RemoveAsync(Guid userId, CancellationToken cancellationToken) =>
        await cache.RemoveAsync(CacheKey(userId), token: cancellationToken).ConfigureAwait(false);

    private static string CacheKey(Guid userId) => $"{CacheKeyPrefix}{userId:N}";
}
