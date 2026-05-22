using System.Collections.Frozen;
using System.Diagnostics;
using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Domain;

namespace Granit.Presence.Notifications;

/// <summary>
/// <see cref="INotificationDeliveryGate"/> that suppresses push channels when the
/// recipient is in <see cref="PresenceStatus.DoNotDisturb"/> or
/// <see cref="PresenceStatus.Offline"/>. Store-and-forward channels (InApp, Email,
/// SMS, WhatsApp, Zulip) always pass — the user can read them later.
/// </summary>
public sealed class PresenceNotificationDeliveryGate(
    IPresenceQueryService presenceQueryService,
    ICurrentTenant currentTenant,
    PresenceMetrics metrics) : INotificationDeliveryGate
{
    private static readonly FrozenSet<string> PushChannels = new[]
        {
            NotificationChannels.SignalR,
            NotificationChannels.Sse,
            NotificationChannels.Push,
            NotificationChannels.MobilePush,
        }
        .ToFrozenSet();

    /// <inheritdoc />
    public async Task<bool> ShouldDeliverAsync(
        string userId,
        string notificationTypeName,
        string channelName,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        if (!PushChannels.Contains(channelName))
        {
            return true;
        }

        if (!Guid.TryParse(userId, out Guid userGuid))
        {
            // External identity (OIDC sub, email, …) cannot be looked up — fail open
            // and let the notification through. Apps that need stricter coupling can
            // register a custom gate.
            return true;
        }

        using Activity? activity = PresenceActivitySource.Source.StartActivity(PresenceActivitySource.GateNotification);
        activity?.SetTag("notifications.type", notificationTypeName);
        activity?.SetTag("notifications.channel", channelName);

        PresenceSnapshot snapshot = await presenceQueryService
            .GetAsync(userGuid, cancellationToken).ConfigureAwait(false);

        bool deliver = snapshot.EffectiveStatus
            is not (PresenceStatus.DoNotDisturb or PresenceStatus.Offline);

        if (!deliver)
        {
            string? tenant = tenantId?.ToString()
                ?? (currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null);
            metrics.RecordNotificationGated(tenant, channelName);
        }

        return deliver;
    }
}
