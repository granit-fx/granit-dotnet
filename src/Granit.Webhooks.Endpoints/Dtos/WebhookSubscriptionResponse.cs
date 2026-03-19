using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Response representing a webhook subscription.
/// </summary>
public sealed record WebhookSubscriptionResponse(
    Guid Id,
    string TargetUrl,
    string EventType,
    WebhookSubscriptionStatus Status,
    int ConsecutiveFailureCount,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
