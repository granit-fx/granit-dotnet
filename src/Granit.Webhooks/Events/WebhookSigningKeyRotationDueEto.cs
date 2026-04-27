using Granit.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Published by the rotation scanner (FU-1b) when a <see cref="Domain.WebhookSigningKey"/>
/// is approaching its expiration window and the owning subscription's administrators
/// should rotate it before the key stops being accepted in verification.
/// </summary>
/// <remarks>
/// <para>
/// Emitted at most once per (key, calendar week). The scanner stamps
/// <see cref="Domain.WebhookSigningKey.LastRotationNotificationAt"/> on every emission
/// so subsequent runs of the same daily job do not re-publish.
/// </para>
/// <para>
/// The rotation lead time is configurable via
/// <see cref="Options.WebhooksOptions.RotationLeadTimeDays"/> (default 14, range 1-90).
/// </para>
/// </remarks>
/// <param name="SubscriptionId">Identifier of the owning <see cref="Domain.WebhookSubscription"/>.</param>
/// <param name="KeyId">Identifier of the signing key approaching expiration.</param>
/// <param name="ExpiresAt">UTC timestamp at which the key stops being accepted in verification.</param>
public sealed record WebhookSigningKeyRotationDueEto(
    Guid SubscriptionId,
    Guid KeyId,
    DateTimeOffset ExpiresAt) : IIntegrationEvent;
