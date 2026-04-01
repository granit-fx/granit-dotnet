using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="EmailConfirmationRequestedEto"/> by sending an email confirmation link.
/// Resolves user email from the identity store (the event intentionally omits PII).
/// </summary>
internal static partial class EmailConfirmationRequestedHandler
{
    public static async Task HandleAsync(
        EmailConfirmationRequestedEto evt,
        IIdentityUserReader userReader,
        IOptions<IdentityNotificationOptions> options,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        IIdentityUser? user = await userReader.GetUserAsync(evt.UserId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        if (user?.Email is null)
        {
            return;
        }

        string confirmLink = options.Value.BuildConfirmEmailUrl(
            evt.UserId.ToString(), evt.Token);

        await publisher.PublishAsync(
            EmailConfirmationNotificationType.Instance,
            new EmailConfirmationNotificationData(user.Email, confirmLink),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
