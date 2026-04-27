using Granit.Authentication.ApiKeys.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Authentication.ApiKeys.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ApiKeyCreatedEto"/> by publishing an
/// <see cref="ApiKeyIssuedNotificationType"/> to every administrator subscribed to it.
/// </summary>
/// <remarks>
/// <para>
/// Recipients are resolved via <see cref="INotificationPublisher.PublishToSubscribersAsync"/>
/// — administrators opt in through the notifications admin UI. This avoids hardcoding
/// addresses in options and naturally supports per-tenant routing: the dispatch engine
/// intersects subscribers with the active tenant context.
/// </para>
/// <para>
/// Wolverine routing: the originating Eto is fired by <c>ApiKeyEntry.Create()</c>,
/// which lives in the same process as the <c>*.Notifications</c> bridge in every Granit
/// host shipping <c>Granit.Authentication.ApiKeys</c>. Wolverine therefore dispatches the
/// message in-process — no outbox / queue round trip. If a future deployment splits the
/// admin UI from the API, the Eto (which implements <c>IIntegrationEvent</c>) will
/// traverse the bus without code changes.
/// </para>
/// <para>
/// Secret hygiene: the originating <see cref="ApiKeyCreatedEto"/> intentionally carries
/// only <c>(ApiKeyId, Name, Type)</c> — never the raw key or its hash. This handler
/// passes those three fields through verbatim.
/// </para>
/// </remarks>
public class ApiKeyIssuedNotificationHandler
{
    public static async Task HandleAsync(
        ApiKeyCreatedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            ApiKeyIssuedNotificationType.Instance,
            new ApiKeyIssuedNotificationData(
                evt.ApiKeyId,
                evt.Name,
                evt.Type),
            cancellationToken).ConfigureAwait(false);
    }
}
