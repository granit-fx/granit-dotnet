namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Response after rotating a subscription's signing secret.
/// </summary>
public sealed record WebhookSubscriptionRotateSecretResponse(string SigningSecret);
