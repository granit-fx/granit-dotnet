using Granit.Hostnames.Domain.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Hostnames.Notifications.Handlers;

/// <summary>
/// Handles <see cref="HostnameCertificateSecuredEto"/> by sending a confirmation notification
/// to the resource owner. Published via the Wolverine distributed event bus.
/// </summary>
public class CertificateSecuredHandler
{
    public static async Task HandleAsync(
        HostnameCertificateSecuredEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            HostnamesCertificateSecuredNotificationType.Instance,
            new HostnamesCertificateSecuredNotificationData(
                evt.HostnameId,
                evt.Host,
                evt.OwnerType,
                evt.OwnerId,
                evt.ExpiresAt),
            [evt.OwnerId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
