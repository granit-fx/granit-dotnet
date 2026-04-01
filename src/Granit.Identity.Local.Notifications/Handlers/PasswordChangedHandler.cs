using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="PasswordChangedEto"/> by sending a security alert email,
/// enabling the user to detect unauthorized password changes (compromise detection).
/// </summary>
internal static partial class PasswordChangedHandler
{
    public static async Task HandleAsync(
        PasswordChangedEto evt,
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
            PasswordChangedNotificationType.Instance,
            new PasswordChangedNotificationData(user.Email),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
