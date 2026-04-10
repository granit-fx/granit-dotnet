using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Handles <see cref="PasswordResetRequestedEto"/> by sending a password reset email
/// with a link containing the reset token.
/// </summary>
public class PasswordResetRequestedHandler
{
    public static async Task HandleAsync(
        PasswordResetRequestedEto evt,
        IOptions<IdentityNotificationOptions> options,
        INotificationPublisher publisher,
        ITenantUrlResolver? urlResolver = null,
        CancellationToken cancellationToken = default)
    {
        IdentityNotificationOptions opts = options.Value;
        string baseUrl = urlResolver is not null
            ? await urlResolver.ResolveBaseUrlAsync(cancellationToken).ConfigureAwait(false)
            : opts.FrontendBaseUrl;

        string resetLink = opts.BuildResetPasswordUrl(
            baseUrl, evt.UserId.ToString(), evt.ResetToken);

        await publisher.PublishAsync(
            PasswordResetNotificationType.Instance,
            new PasswordResetNotificationData(evt.Email, resetLink),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
