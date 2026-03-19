namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Request to create a new webhook subscription.
/// </summary>
public sealed record WebhookSubscriptionCreateRequest(string TargetUrl, string EventType);
