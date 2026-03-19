namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Request to update an existing webhook subscription's target URL.
/// </summary>
public sealed record WebhookSubscriptionUpdateRequest(string TargetUrl);
