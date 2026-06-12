using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Granit.MultiTenancy;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Presence.Internal;

/// <summary>
/// <see cref="IResourcePresenceTracker"/> implementation backed by <see cref="IFusionCache"/>.
/// </summary>
/// <remarks>
/// <para>
/// Cache key shape: <c>granit.presence.room:{tenantId}:{kind}:{id}</c>. <c>tenantId</c>
/// coalesces to <c>"global"</c> when no tenant context is available — keeps the rooms
/// tenant-scoped without requiring a hard <see cref="ICurrentTenant"/> dependency.
/// </para>
/// <para>
/// Concurrency: under high contention two parallel <c>JoinAsync</c> calls may race on the
/// read-modify-write; the MAX-merge on timestamps makes the loss self-healing on the next
/// heartbeat. FusionCache's L1 lock keeps the writes consistent on a single node; the L2
/// backplane (when wired) propagates the final state.
/// </para>
/// </remarks>
internal sealed class FusionCacheResourcePresenceTracker(
    IFusionCache cache,
    IClock clock,
    ICurrentTenant currentTenant,
    IOptionsMonitor<PresenceOptions> options,
    PresenceMetrics metrics) : IResourcePresenceTracker
{
    private const string CacheKeyPrefix = "granit.presence.room";
    private const string GlobalTenant = "global";
    private const string RoomSizeTag = "room.size";

    /// <summary>Maximum metadata size in bytes (UTF-8) accepted by <see cref="JoinAsync"/>.</summary>
    public const int MaxMetadataBytes = 512;

    public async Task<ResourceRoom> JoinAsync(
        ResourceRef resource,
        Guid userId,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        resource.Validate();

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        int metadataBytes = metadata is null ? 0 : Encoding.UTF8.GetByteCount(metadata);
        if (metadataBytes > MaxMetadataBytes)
        {
            throw new ArgumentException(
                $"Metadata size {metadataBytes} bytes exceeds the maximum of {MaxMetadataBytes} bytes.",
                nameof(metadata));
        }

        string? tenantId = ResolveTenantTag();

        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.RoomJoin);
        EnrichActivity(activity, resource, tenantId);

        PresenceOptions opts = options.CurrentValue;
        DateTimeOffset nowUtc = clock.Now;
        string key = CacheKey(resource, tenantId);

        List<ResourcePresenceEntry>? existing = await cache
            .GetOrDefaultAsync<List<ResourcePresenceEntry>?>(key, defaultValue: null, token: cancellationToken)
            .ConfigureAwait(false);

        List<ResourcePresenceEntry> merged = MergeJoin(existing, userId, nowUtc, metadata, opts.OfflineThreshold);

        await cache.SetAsync(key, merged, opts.HeartbeatCacheTtl, token: cancellationToken).ConfigureAwait(false);

        metrics.RecordRoomJoin(tenantId, resource.Kind);
        metrics.RecordRoomSize(tenantId, resource.Kind, merged.Count);
        if (metadata is not null)
        {
            metrics.RecordRoomMetadataBytes(resource.Kind, metadataBytes);
        }
        activity?.SetTag(RoomSizeTag, merged.Count);

        return new ResourceRoom(resource, merged);
    }

    public async Task LeaveAsync(
        ResourceRef resource,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        resource.Validate();

        string? tenantId = ResolveTenantTag();
        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.RoomLeave);
        EnrichActivity(activity, resource, tenantId);

        string key = CacheKey(resource, tenantId);

        List<ResourcePresenceEntry>? existing = await cache
            .GetOrDefaultAsync<List<ResourcePresenceEntry>?>(key, defaultValue: null, token: cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || existing.Count == 0)
        {
            activity?.SetTag(RoomSizeTag, 0);
            return;
        }

        PresenceOptions opts = options.CurrentValue;
        DateTimeOffset cutoff = clock.Now - opts.OfflineThreshold;

        int staleDropped = 0;
        bool removedSelf = false;
        List<ResourcePresenceEntry> remaining = new(existing.Count);
        foreach (ResourcePresenceEntry e in existing)
        {
            if (e.UserId == userId)
            {
                removedSelf = true;
                continue;
            }

            if (e.LastSeenUtc >= cutoff)
            {
                remaining.Add(e);
            }
            else
            {
                staleDropped++;
            }
        }

        if (removedSelf)
        {
            metrics.RecordRoomLeave(tenantId, resource.Kind, PresenceMetrics.ReasonExplicit);
        }
        for (int i = 0; i < staleDropped; i++)
        {
            metrics.RecordRoomLeave(tenantId, resource.Kind, PresenceMetrics.ReasonStale);
        }

        if (remaining.Count == 0)
        {
            await cache.RemoveAsync(key, token: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await cache.SetAsync(key, remaining, opts.HeartbeatCacheTtl, token: cancellationToken).ConfigureAwait(false);
        }

        metrics.RecordRoomSize(tenantId, resource.Kind, remaining.Count);
        activity?.SetTag(RoomSizeTag, remaining.Count);
    }

    public async Task<ResourceRoom> GetAsync(
        ResourceRef resource,
        CancellationToken cancellationToken = default)
    {
        resource.Validate();

        string? tenantId = ResolveTenantTag();
        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.RoomGet);
        EnrichActivity(activity, resource, tenantId);

        string key = CacheKey(resource, tenantId);

        List<ResourcePresenceEntry>? existing = await cache
            .GetOrDefaultAsync<List<ResourcePresenceEntry>?>(key, defaultValue: null, token: cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || existing.Count == 0)
        {
            activity?.SetTag(RoomSizeTag, 0);
            metrics.RecordRoomSize(tenantId, resource.Kind, 0);
            return new ResourceRoom(resource, []);
        }

        PresenceOptions opts = options.CurrentValue;
        DateTimeOffset cutoff = clock.Now - opts.OfflineThreshold;

        List<ResourcePresenceEntry> live = new(existing.Count);
        foreach (ResourcePresenceEntry e in existing)
        {
            if (e.LastSeenUtc >= cutoff)
            {
                live.Add(e);
            }
        }

        metrics.RecordRoomSize(tenantId, resource.Kind, live.Count);
        activity?.SetTag(RoomSizeTag, live.Count);

        return new ResourceRoom(resource, live);
    }

    /// <summary>
    /// Applies the MAX anti-flapping rule + multi-tab dedup. Existing stale entries are
    /// pruned in the same pass so the cache value doesn't grow unbounded under churn.
    /// </summary>
    private static List<ResourcePresenceEntry> MergeJoin(
        List<ResourcePresenceEntry>? existing,
        Guid userId,
        DateTimeOffset nowUtc,
        string? newMetadata,
        TimeSpan offlineThreshold)
    {
        DateTimeOffset cutoff = nowUtc - offlineThreshold;
        List<ResourcePresenceEntry> merged = existing is null ? [] : new List<ResourcePresenceEntry>(existing.Count + 1);

        bool seen = false;
        if (existing is not null)
        {
            foreach (ResourcePresenceEntry e in existing)
            {
                if (e.UserId == userId)
                {
                    seen = true;
                    // MAX rule on LastSeenUtc; last metadata wins.
                    DateTimeOffset maxSeen = e.LastSeenUtc > nowUtc ? e.LastSeenUtc : nowUtc;
                    merged.Add(new ResourcePresenceEntry(userId, maxSeen, newMetadata));
                }
                else if (e.LastSeenUtc >= cutoff)
                {
                    merged.Add(e);
                }
                // stale → drop
            }
        }

        if (!seen)
        {
            merged.Add(new ResourcePresenceEntry(userId, nowUtc, newMetadata));
        }

        return merged;
    }

    private string? ResolveTenantTag() =>
        currentTenant.IsAvailable && currentTenant.Id is { } id ? id.ToString("N") : null;

    private static string CacheKey(ResourceRef resource, string? tenantId) =>
        $"{CacheKeyPrefix}:{tenantId ?? GlobalTenant}:{resource.Kind}:{resource.Id}";

    /// <summary>
    /// Stamps the activity with the resource tags. Long ids are SHA256-hashed (first 16 hex
    /// chars) to keep span cardinality bounded — full ids still live in the cache key.
    /// </summary>
    private static void EnrichActivity(Activity? activity, ResourceRef resource, string? tenantId)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("resource.kind", resource.Kind);
        activity.SetTag("resource.id", HashIfLong(resource.Id));
        activity.SetTag("tenant.id", tenantId ?? GlobalTenant);
    }

    private static string HashIfLong(string id)
    {
        if (id.Length <= 32)
        {
            return id;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return Convert.ToHexString(hash, 0, 8); // 8 bytes → 16 hex chars
    }
}
