using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="TwoFactorEmailOtpRequestedEto"/> by emailing the one-time code
/// used to complete the email-based two-factor method.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
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
