namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Response representing a registered webhook event type.
/// </summary>
public sealed record WebhookEventTypeResponse(
    string EventType,
    string? DisplayName,
    string? Description,
    string? Category);
