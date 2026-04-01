using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="TwoFactorChangedEto"/> by sending a security alert when
/// two-factor authentication is enabled or disabled on a user account.
/// </summary>
internal static partial class TwoFactorChangedHandler
{
    public static async Task HandleAsync(
        TwoFactorChangedEto evt,
        IIdentityUserReader userReader,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        IIdentityUser? user = await userReader.GetUserAsync(evt.UserId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        if (user?.Email is null)
        {
            return;
        }

        await publisher.PublishAsync(
            TwoFactorChangedNotificationType.Instance,
            new TwoFactorChangedNotificationData(user.Email, evt.Enabled),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
