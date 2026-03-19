namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Request to deactivate a webhook subscription with a mandatory reason.
/// </summary>
public sealed record WebhookSubscriptionDeactivateRequest(string Reason);
