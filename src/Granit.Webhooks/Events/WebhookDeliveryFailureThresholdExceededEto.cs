using Granit.Core.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Published when consecutive webhook delivery failures reach the threshold (5).
/// Enables SLA alerting before automatic suspension.
/// </summary>
/// <param name="SubscriptionId">The unique identifier of the subscription.</param>
/// <param name="TargetUrl">The HTTPS endpoint that is failing.</param>
/// <param name="ConsecutiveFailureCount">Number of consecutive failures.</param>
public sealed record WebhookDeliveryFailureThresholdExceededEto(
    Guid SubscriptionId,
    string TargetUrl,
    int ConsecutiveFailureCount) : IIntegrationEvent;
