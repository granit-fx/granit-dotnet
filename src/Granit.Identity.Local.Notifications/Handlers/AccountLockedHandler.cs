using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="AccountLockedEto"/> by sending a lockout alert via email and in-app
/// notification, so the user is aware of the brute-force attempt.
/// </summary>
internal static partial class AccountLockedHandler
{
    public static async Task HandleAsync(
        AccountLockedEto evt,
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
            AccountLockedNotificationType.Instance,
            new AccountLockedNotificationData(user.Email, evt.FailedAttempts),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
