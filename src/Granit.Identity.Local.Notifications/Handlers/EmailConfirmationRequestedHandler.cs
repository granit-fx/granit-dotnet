using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="EmailConfirmationRequestedEto"/> by sending an email confirmation link.
/// Resolves user email from the identity store (the event intentionally omits PII).
/// </summary>
public class EmailConfirmationRequestedHandler
{
    public static async Task HandleAsync(
        EmailConfirmationRequestedEto evt,
        IIdentityUserReader userReader,
        IOptions<IdentityNotificationOptions> options,
        INotificationPublisher publisher,
        ITenantUrlResolver? urlResolver = null,
        CancellationToken cancellationToken = default)
    {
        IIdentityUser? user = await userReader.GetUserAsync(evt.UserId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        if (user?.Email is null)
        {
            return;
        }

        IdentityNotificationOptions opts = options.Value;
        string baseUrl = urlResolver is not null
            ? await urlResolver.ResolveBaseUrlAsync(cancellationToken).ConfigureAwait(false)
            : opts.FrontendBaseUrl;

        string confirmLink = opts.BuildConfirmEmailUrl(
            baseUrl, evt.UserId.ToString(), evt.Token);

        await publisher.PublishAsync(
            EmailConfirmationNotificationType.Instance,
            new EmailConfirmationNotificationData(user.Email, confirmLink),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
