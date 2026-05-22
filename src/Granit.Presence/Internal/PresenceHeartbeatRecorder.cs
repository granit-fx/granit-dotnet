using System.Diagnostics;
using Granit.Events;
using Granit.MultiTenancy;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Domain;
using Granit.Presence.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Presence.Internal;

internal sealed partial class PresenceHeartbeatRecorder(
    IPresenceTracker tracker,
    IPresenceStore store,
    PresenceQueryService queryService,
    IDistributedEventBus eventBus,
    ICurrentTenant currentTenant,
    IClock clock,
    PresenceMetrics metrics,
    ILogger<PresenceHeartbeatRecorder> logger) : IPresenceHeartbeatRecorder
{
    public async Task<PresenceSnapshot> RecordAsync(
        Guid userId,
        TimeSpan idleDuration,
        CancellationToken cancellationToken)
    {
        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.RecordPoll);
        activity?.SetTag("presence.user_id", userId);

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordHeartbeat(tenantId);

        UserPresence? presence = await store.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        PresenceHeartbeat? previousHeartbeat = await tracker.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        PresenceSnapshot previous = queryService.Compose(userId, presence, previousHeartbeat);

        PresenceHeartbeat merged = await tracker
            .RecordPollAsync(userId, idleDuration, cancellationToken).ConfigureAwait(false);
        PresenceSnapshot current = queryService.Compose(userId, presence, merged);

        if (previous.EffectiveStatus != current.EffectiveStatus)
        {
            metrics.RecordStatusChanged(tenantId, previous.EffectiveStatus, current.EffectiveStatus);
            await PublishChangeAsync(previous, current, cancellationToken).ConfigureAwait(false);
            LogTransition(logger, userId, previous.EffectiveStatus, current.EffectiveStatus);
        }

        return current;
    }

    private Task PublishChangeAsync(PresenceSnapshot previous, PresenceSnapshot current, CancellationToken ct) =>
        eventBus.PublishAsync(
            new UserPresenceChangedEto(previous.UserId, previous.EffectiveStatus, current.EffectiveStatus, clock.Now),
            ct);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Presence transition for user {UserId}: {FromStatus} -> {ToStatus}.")]
    private static partial void LogTransition(ILogger logger, Guid userId, PresenceStatus fromStatus, PresenceStatus toStatus);
}
