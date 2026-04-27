using Granit.Authentication.ApiKeys.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Authentication.ApiKeys.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ApiKeyRevokedEto"/> by publishing an
/// <see cref="ApiKeyRevokedNotificationType"/> to every administrator subscribed to it.
/// </summary>
/// <remarks>
/// <para>
/// Recipients are resolved via <see cref="INotificationPublisher.PublishToSubscribersAsync"/>.
/// Wolverine dispatches the originating Eto in-process when the API and the notifications
/// bridge ship together; if the bridge is later moved out-of-process, the Eto traverses
/// the bus without code changes.
/// </para>
/// <para>
/// Secret hygiene: the originating <see cref="ApiKeyRevokedEto"/> carries a
/// <c>HashedKey</c> field used by the cache-eviction handler. That hash is
/// intentionally NOT propagated to the notification payload.
/// </para>
/// </remarks>
public class ApiKeyRevokedNotificationHandler
{
    public static async Task HandleAsync(
        ApiKeyRevokedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            ApiKeyRevokedNotificationType.Instance,
            new ApiKeyRevokedNotificationData(evt.ApiKeyId),
            cancellationToken).ConfigureAwait(false);
    }
}
