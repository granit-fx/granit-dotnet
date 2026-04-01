using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="UserRegisteredEto"/> by sending a welcome notification to the new user.
/// </summary>
internal static partial class UserRegisteredHandler
{
    public static async Task HandleAsync(
        UserRegisteredEto evt,
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
            WelcomeNotificationType.Instance,
            new WelcomeNotificationData(user.Email),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
