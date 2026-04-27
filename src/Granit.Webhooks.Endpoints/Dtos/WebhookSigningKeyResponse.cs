using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Read-only projection of a <see cref="WebhookSigningKey"/>. The protected secret is
/// intentionally NOT included — clients receive the plain-text secret only at creation
/// time via <see cref="WebhookSigningKeyCreatedResponse"/>.
/// </summary>
public sealed record WebhookSigningKeyResponse(
    Guid Id,
    Guid SubscriptionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastRotationNotificationAt,
    WebhookSigningKeyStatus Status);

/// <summary>
/// Response after creating (rotating) a new signing key. The <see cref="PlainSecret"/>
/// is returned exactly once — store it securely.
/// </summary>
public sealed record WebhookSigningKeyCreatedResponse(
    Guid Id,
    Guid SubscriptionId,
    DateTimeOffset CreatedAt,
    string PlainSecret);
