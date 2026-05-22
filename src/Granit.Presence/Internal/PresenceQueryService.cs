using System.Diagnostics;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Domain;
using Granit.Presence.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.Presence.Internal;

internal sealed class PresenceQueryService(
    IPresenceStore store,
    IPresenceTracker tracker,
    IClock clock,
    IOptionsMonitor<PresenceOptions> options) : IPresenceQueryService
{
    public async Task<PresenceSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.QueryEffective);

        UserPresence? presence = await store.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        PresenceHeartbeat? heartbeat = await tracker.GetAsync(userId, cancellationToken).ConfigureAwait(false);

        return Compose(userId, presence, heartbeat);
    }

    public async Task<IReadOnlyDictionary<Guid, PresenceSnapshot>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.QueryEffective);
        activity?.SetTag("presence.user_count", userIds.Count);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, PresenceSnapshot>(0);
        }

        IReadOnlyDictionary<Guid, UserPresence> presences = await store
            .GetManyAsync(userIds, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, PresenceHeartbeat> heartbeats = await tracker
            .GetManyAsync(userIds, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, PresenceSnapshot> result = new(userIds.Count);
        foreach (Guid userId in userIds)
        {
            presences.TryGetValue(userId, out UserPresence? presence);
            heartbeats.TryGetValue(userId, out PresenceHeartbeat? heartbeat);
            result[userId] = Compose(userId, presence, heartbeat);
        }

        return result;
    }

    internal PresenceSnapshot Compose(Guid userId, UserPresence? presence, PresenceHeartbeat? heartbeat)
    {
        PresenceOptions opts = options.CurrentValue;
        DateTimeOffset nowUtc = clock.Now;
        DateTimeOffset lastSeen = heartbeat?.LastPollUtc ?? DateTimeOffset.MinValue;

        ManualPresenceStatus? activeOverride = presence?.GetActiveOverride(clock);
        DateTimeOffset? overrideUntil = activeOverride is null ? null : presence!.OverrideUntilUtc;

        PresenceStatus effective = activeOverride switch
        {
            ManualPresenceStatus.AppearOffline => PresenceStatus.Offline,
            ManualPresenceStatus.DoNotDisturb => PresenceStatus.DoNotDisturb,
            ManualPresenceStatus.Busy => PresenceStatus.Busy,
            _ => DeriveConnectivity(heartbeat, nowUtc, opts),
        };

        return new PresenceSnapshot(userId, effective, activeOverride, overrideUntil, lastSeen);
    }

    private static PresenceStatus DeriveConnectivity(PresenceHeartbeat? heartbeat, DateTimeOffset nowUtc, PresenceOptions opts)
    {
        if (heartbeat is null)
        {
            return PresenceStatus.Offline;
        }

        TimeSpan sincePoll = nowUtc - heartbeat.LastPollUtc;
        if (sincePoll > opts.OfflineThreshold)
        {
            return PresenceStatus.Offline;
        }

        TimeSpan sinceActivity = nowUtc - heartbeat.LastActivityUtc;
        return sinceActivity > opts.AwayThreshold
            ? PresenceStatus.Away
            : PresenceStatus.Online;
    }
}
