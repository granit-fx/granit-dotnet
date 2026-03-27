namespace Granit.Webhooks.Internal;

/// <summary>
/// Shared constants for the Granit.Webhooks module.
/// </summary>
internal static class WebhooksConstants
{
    /// <summary>Named HttpClient registered for webhook delivery.</summary>
    internal const string HttpClientName = "granit-webhook-delivery";

    /// <summary>Wolverine local queue for <see cref="Messages.SendWebhookCommand"/>.</summary>
    internal const string DeliveryQueueName = "webhook-delivery";

    /// <summary>Webhook envelope contract version included in every HTTP payload.</summary>
    internal const string ApiVersion = "2025-01-01";

    /// <summary>Wolverine local queue for <see cref="Messages.WebhookTrigger"/> fan-out.</summary>
    internal const string FanoutQueueName = "webhook-fanout";

    /// <summary>Bounded channel capacity for <see cref="Messages.WebhookTrigger"/> (in-process dispatch).</summary>
    internal const int TriggerChannelCapacity = 1_000;

    /// <summary>Bounded channel capacity for <see cref="Messages.SendWebhookCommand"/> (in-process dispatch).</summary>
    internal const int CommandChannelCapacity = 5_000;

    /// <summary>Minimum retention period for delivery audit records (ISO 27001).</summary>
    internal static readonly TimeSpan MinAuditRetention = TimeSpan.FromDays(3 * 365);
}
