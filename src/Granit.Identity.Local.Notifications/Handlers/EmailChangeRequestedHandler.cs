using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.MultiTenancy;
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
public class EmailChangeRequestedHandler
{
    public static async Task HandleAsync(
        EmailChangeRequestedEto evt,
        IOptions<IdentityNotificationOptions> options,
        INotificationPublisher publisher,
        ITenantUrlResolver? urlResolver = null,
        CancellationToken cancellationToken = default)
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
        IdentityNotificationOptions opts = options.Value;
        string? resolvedUrl = urlResolver is not null
            ? await urlResolver.ResolveBaseUrlAsync(cancellationToken).ConfigureAwait(false)
            : null;
        string baseUrl = !string.IsNullOrEmpty(resolvedUrl) ? resolvedUrl : opts.FrontendBaseUrl;

        string confirmLink = opts.BuildChangeEmailUrl(
            baseUrl, userId, evt.NewEmail, evt.Token);

        await publisher.PublishAsync(
            EmailChangeConfirmationNotificationType.Instance,
            new EmailChangeConfirmationNotificationData(evt.NewEmail, confirmLink),
            [userId],
            new RecipientInfo { UserId = userId, Email = evt.NewEmail },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
