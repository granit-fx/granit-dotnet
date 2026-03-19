using Granit.Core.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Published when a webhook subscription reaches 5 consecutive delivery failures,
/// indicating the target endpoint may be unreachable or misconfigured.
/// </summary>
public sealed record WebhookDeliveryFailureThresholdExceededEto(
    Guid SubscriptionId,
    string TargetUrl,
    int ConsecutiveFailureCount) : IIntegrationEvent;
