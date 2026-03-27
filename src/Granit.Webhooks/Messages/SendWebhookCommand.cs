namespace Granit.Webhooks.Messages;

/// <summary>
/// Wolverine command representing a single webhook delivery attempt to one subscriber.
/// </summary>
/// <remarks>
/// <para>
/// Produced by <see cref="Handlers.WebhookFanoutHandler"/> — one instance per active
/// subscription for a given <see cref="WebhookTrigger"/>. Each command is persisted
/// individually in the Outbox, so retries and failures are isolated per subscriber.
/// </para>
/// <para>
/// The signing secret is NOT carried in this command to avoid persisting secrets in the
/// Wolverine outbox. The <see cref="Handlers.SendWebhookHandler"/> resolves the secret
/// at delivery time from the subscription via <see cref="Abstractions.IWebhookSecretProtector"/>.
/// </para>
/// </remarks>
public sealed record SendWebhookCommand
{
    /// <summary>
    /// Unique identifier of this delivery attempt.
    /// Distinct from <see cref="WebhookEnvelope.EventId"/> — one event produces N delivery IDs.
    /// Used as the primary key in <see cref="Domain.WebhookDeliveryAttempt"/> (ISO 27001 audit trail).
    /// </summary>
    public required Guid DeliveryId { get; init; }

    /// <summary>Identifier of the target <see cref="Domain.WebhookSubscription"/>.</summary>
    public required Guid SubscriptionId { get; init; }

    /// <summary>Target HTTPS endpoint URL.</summary>
    public required string TargetUrl { get; init; }

    /// <summary>The standardized envelope to serialize and POST to <see cref="TargetUrl"/>.</summary>
    public required WebhookEnvelope Envelope { get; init; }
}
