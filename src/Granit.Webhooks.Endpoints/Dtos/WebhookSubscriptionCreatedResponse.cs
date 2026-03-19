using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Response after creating a subscription — includes the signing secret (returned once).
/// </summary>
public sealed record WebhookSubscriptionCreatedResponse(
    Guid Id,
    string TargetUrl,
    string EventType,
    WebhookSubscriptionStatus Status,
    string SigningSecret);
