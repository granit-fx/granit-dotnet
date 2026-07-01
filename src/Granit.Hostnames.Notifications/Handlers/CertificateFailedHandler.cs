using Granit.Hostnames.Domain.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Hostnames.Notifications.Handlers;

/// <summary>
/// Handles <see cref="HostnameCertificateFailedEto"/> by sending an alert notification
/// to the resource owner. Published via the Wolverine distributed event bus.
/// </summary>
public class CertificateFailedHandler
{
    public static async Task HandleAsync(
        HostnameCertificateFailedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            HostnamesCertificateFailedNotificationType.Instance,
            new HostnamesCertificateFailedNotificationData(
                evt.HostnameId,
                evt.Host,
                evt.OwnerType,
                evt.OwnerId),
            [evt.OwnerId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
