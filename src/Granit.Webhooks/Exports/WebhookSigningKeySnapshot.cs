using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Exports;

/// <summary>
/// Lifecycle metadata snapshot of a <see cref="WebhookSigningKey"/> for export purposes.
/// </summary>
/// <remarks>
/// Intentionally excludes <see cref="WebhookSigningKey.ProtectedSecret"/> — secret material
/// must not leave the database boundary. Operators should rotate keys on the target instance
/// after a migration rather than transferring protected secrets via export files.
/// </remarks>
public sealed record WebhookSigningKeySnapshot(
    Guid Id,
    WebhookSigningKeyStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt);
