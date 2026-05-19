using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="UserImpersonatedEto"/> by sending a transparency notification
/// to the impersonated user (GDPR/SOC2 compliance).
/// </summary>
public class UserImpersonatedHandler
{
    public static async Task HandleAsync(
        UserImpersonatedEto evt,
        IIdentityUserReader userReader,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        IIdentityUser? impersonator = await userReader
            .GetUserAsync(evt.ImpersonatorId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        string? displayName = BuildDisplayName(impersonator);

        await publisher.PublishAsync(
            ImpersonationAlertNotificationType.Instance,
            new ImpersonationAlertNotificationData(evt.OccurredAt, displayName),
            [evt.TargetUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }

    private static string? BuildDisplayName(IIdentityUser? user)
    {
        if (user is null)
        {
            return null;
        }

        string name = $"{user.FirstName} {user.LastName}".Trim();
        return name.Length > 0 ? name : user.Username;
    }
}
