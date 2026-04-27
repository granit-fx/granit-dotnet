using Granit.Authentication.ApiKeys.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Authentication.ApiKeys.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ApiKeyExpiringSoonEto"/> by publishing an
/// <see cref="ApiKeyExpiringSoonNotificationType"/> to every administrator
/// subscribed to it.
/// </summary>
/// <remarks>
/// <para>
/// Recipients are resolved via
/// <see cref="INotificationPublisher.PublishToSubscribersAsync"/> — administrators
/// opt in through the notifications admin UI.
/// </para>
/// <para>
/// Wolverine routing: the originating Eto is published from the daily scanner in
/// <c>Granit.Authentication.ApiKeys.BackgroundJobs</c>, which lives in the same
/// process as this bridge in every Granit host shipping
/// <c>Granit.Authentication.ApiKeys</c>. Wolverine therefore dispatches in-process —
/// no outbox round trip. If a future deployment splits the admin UI from the API,
/// the Eto traverses the bus without code changes.
/// </para>
/// <para>
/// Secret hygiene: the originating Eto carries only
/// <c>(KeyId, KeyName, KeyType, ExpiresAt, TenantId)</c> — never the raw key, its
/// hash, or the prefix. This handler computes <c>DaysUntilExpiry</c> from
/// <see cref="TimeProvider.GetUtcNow"/> for template UX and forwards the rest
/// verbatim.
/// </para>
/// </remarks>
public class ApiKeyExpiringSoonNotificationHandler
{
    public static async Task HandleAsync(
        ApiKeyExpiringSoonEto evt,
        INotificationPublisher publisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // Whole-day countdown for the template. Clamp at 0 — if the scanner caught a
        // key already past expiration (rare race), the message still reads sensibly.
        TimeSpan remaining = evt.ExpiresAt - timeProvider.GetUtcNow();
        int daysUntilExpiry = remaining.TotalDays > 0 ? (int)Math.Ceiling(remaining.TotalDays) : 0;

        await publisher.PublishToSubscribersAsync(
            ApiKeyExpiringSoonNotificationType.Instance,
            new ApiKeyExpiringSoonNotificationData(
                evt.KeyId,
                evt.KeyName,
                evt.KeyType,
                evt.ExpiresAt,
                daysUntilExpiry),
            cancellationToken).ConfigureAwait(false);
    }
}
