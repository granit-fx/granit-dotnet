using System.Diagnostics;
using Granit.Events;
using Granit.MultiTenancy;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Domain;
using Granit.Presence.Events;
using Granit.Timing;

namespace Granit.Presence.Internal;

internal sealed class PresenceOverrideService(
    IPresenceStore store,
    IPresenceTracker tracker,
    PresenceQueryService queryService,
    IDistributedEventBus eventBus,
    ICurrentTenant currentTenant,
    IClock clock,
    PresenceMetrics metrics) : IPresenceOverrideService
{
    public Task<PresenceSnapshot> SetAsync(
        Guid userId,
        ManualPresenceStatus status,
        DateTimeOffset? untilUtc,
        CancellationToken cancellationToken) =>
        MutateAsync(userId, p => p.SetOverride(status, untilUtc, clock), status, untilUtc is not null, cancellationToken);

    public Task<PresenceSnapshot> ClearAsync(Guid userId, CancellationToken cancellationToken) =>
        MutateAsync(userId, p => p.ClearOverride(clock), ManualPresenceStatus.Available, hasUntil: false, cancellationToken);

    private async Task<PresenceSnapshot> MutateAsync(
        Guid userId,
        Action<UserPresence> mutator,
        ManualPresenceStatus targetStatus,
        bool hasUntil,
        CancellationToken cancellationToken)
    {
        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.SetOverride);
        activity?.SetTag("presence.user_id", userId);
        activity?.SetTag("presence.status", targetStatus.ToString());

        UserPresence? existing = await store.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        PresenceHeartbeat? heartbeat = await tracker.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        PresenceSnapshot previous = queryService.Compose(userId, existing, heartbeat);

        UserPresence saved = await store
            .MutateAsync(userId, () => UserPresence.Create(userId, clock), mutator, cancellationToken)
            .ConfigureAwait(false);

        PresenceSnapshot current = queryService.Compose(userId, saved, heartbeat);
        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordOverrideSet(tenantId, targetStatus, hasUntil);

        if (previous.EffectiveStatus != current.EffectiveStatus)
        {
            metrics.RecordStatusChanged(tenantId, previous.EffectiveStatus, current.EffectiveStatus);
            await eventBus.PublishAsync(
                new UserPresenceChangedEto(userId, previous.EffectiveStatus, current.EffectiveStatus, clock.Now),
                cancellationToken).ConfigureAwait(false);
        }

        return current;
    }
}
