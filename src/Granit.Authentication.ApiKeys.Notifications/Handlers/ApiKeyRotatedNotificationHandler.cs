using Granit.Authentication.ApiKeys.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Authentication.ApiKeys.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ApiKeyRotatedEto"/> by publishing an
/// <see cref="ApiKeyRotatedNotificationType"/> to every administrator subscribed to it.
/// </summary>
/// <remarks>
/// <para>
/// Recipients are resolved via <see cref="INotificationPublisher.PublishToSubscribersAsync"/>.
/// Wolverine dispatches the originating Eto in-process when the API and the notifications
/// bridge ship together (the default Granit host topology); if the bridge is later moved
/// out-of-process, the Eto traverses the bus without code changes.
/// </para>
/// <para>
/// Secret hygiene: the originating <see cref="ApiKeyRotatedEto"/> carries an
/// <c>OldHashedKey</c> field used by the cache-eviction handler. That hash is
/// intentionally NOT propagated to the notification payload — recipients have no use
/// for it and excluding it avoids leaking sensitive material into transcripts.
/// </para>
/// </remarks>
public class ApiKeyRotatedNotificationHandler
{
    public static async Task HandleAsync(
        ApiKeyRotatedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            ApiKeyRotatedNotificationType.Instance,
            new ApiKeyRotatedNotificationData(
                evt.OldApiKeyId,
                evt.NewApiKeyId),
            cancellationToken).ConfigureAwait(false);
    }
}
