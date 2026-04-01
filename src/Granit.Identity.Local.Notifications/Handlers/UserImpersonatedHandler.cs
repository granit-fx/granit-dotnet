using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="UserImpersonatedEto"/> by sending a transparency notification
/// to the impersonated user (GDPR/SOC2 compliance).
/// </summary>
internal static partial class UserImpersonatedHandler
{
    public static async Task HandleAsync(
        UserImpersonatedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            ImpersonationAlertNotificationType.Instance,
            new ImpersonationAlertNotificationData(evt.OccurredAt),
            [evt.TargetUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
