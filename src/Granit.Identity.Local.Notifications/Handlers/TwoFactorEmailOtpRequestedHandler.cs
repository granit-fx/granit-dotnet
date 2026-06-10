using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="TwoFactorEmailOtpRequestedEto"/> by emailing the one-time code
/// used to complete the email-based two-factor method.
/// </summary>
public class TwoFactorEmailOtpRequestedHandler
{
    public static async Task HandleAsync(
        TwoFactorEmailOtpRequestedEto evt,
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
            TwoFactorEmailOtpNotificationType.Instance,
            new TwoFactorEmailOtpNotificationData(user.Email, evt.Code),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
