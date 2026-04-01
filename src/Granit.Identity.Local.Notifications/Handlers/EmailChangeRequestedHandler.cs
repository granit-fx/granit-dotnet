using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="EmailChangeRequestedEto"/> by publishing two notifications:
/// <list type="bullet">
///   <item><see cref="EmailChangeAlertNotificationType"/> to the <b>current</b> email (security alert).</item>
///   <item><see cref="EmailChangeConfirmationNotificationType"/> to the <b>new</b> email (confirmation link)
///     via <see cref="RecipientInfo"/> override.</item>
/// </list>
/// </summary>
internal static partial class EmailChangeRequestedHandler
{
    public static async Task HandleAsync(
        EmailChangeRequestedEto evt,
        IOptions<IdentityNotificationOptions> options,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        string userId = evt.UserId.ToString();

        // Alert to the current email (standard recipient resolution)
        await publisher.PublishAsync(
            EmailChangeAlertNotificationType.Instance,
            new EmailChangeAlertNotificationData(evt.CurrentEmail, evt.NewEmail),
            [userId],
            cancellationToken).ConfigureAwait(false);

        // Confirmation to the new email — override recipient so the email
        // goes to the new address, not the one currently on file.
        string confirmLink = options.Value.BuildChangeEmailUrl(
            userId, evt.NewEmail, evt.Token);

        await publisher.PublishAsync(
            EmailChangeConfirmationNotificationType.Instance,
            new EmailChangeConfirmationNotificationData(evt.NewEmail, confirmLink),
            [userId],
            new RecipientInfo { UserId = userId, Email = evt.NewEmail },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
