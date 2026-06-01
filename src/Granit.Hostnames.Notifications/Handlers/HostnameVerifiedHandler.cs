using Granit.Hostnames.Domain.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Hostnames.Notifications.Handlers;

/// <summary>
/// Handles <see cref="HostnameVerifiedEto"/> by sending a confirmation notification
/// to the resource owner. Published via the Wolverine distributed event bus.
/// </summary>
public class HostnameVerifiedHandler
{
    public static async Task HandleAsync(
        HostnameVerifiedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            HostnameVerifiedNotificationType.Instance,
            new HostnameVerifiedNotificationData(
                evt.HostnameId,
                evt.Host,
                evt.OwnerType,
                evt.OwnerId),
            [evt.OwnerId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
