namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Response for a test webhook ping.
/// </summary>
public sealed record WebhookSubscriptionTestPingResponse(bool Success, int HttpStatusCode, long DurationMs);
