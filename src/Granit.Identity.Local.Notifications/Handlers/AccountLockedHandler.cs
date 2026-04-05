using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="AccountLockedEto"/> by sending a lockout alert via email and in-app
/// notification with a password reset link, so the user can immediately regain access
/// without waiting for the lockout to expire or contacting support.
/// </summary>
public class AccountLockedHandler
{
    public static async Task HandleAsync(
        AccountLockedEto evt,
        IOptions<IdentityNotificationOptions> options,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        string resetLink = options.Value.BuildResetPasswordUrl(
            evt.UserId.ToString(), evt.ResetToken);

        await publisher.PublishAsync(
            AccountLockedNotificationType.Instance,
            new AccountLockedNotificationData(evt.Email, evt.FailedAttempts, resetLink, evt.LockoutEndUtc),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
