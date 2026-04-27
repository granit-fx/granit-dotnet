using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Read operations for <see cref="WebhookSigningKey"/> entities.
/// </summary>
public interface IWebhookSigningKeyReader
{
    /// <summary>
    /// Returns all signing keys for the given subscription, regardless of status.
    /// Includes <see cref="WebhookSigningKeyStatus.Active"/>, <see cref="WebhookSigningKeyStatus.Retired"/>,
    /// and <see cref="WebhookSigningKeyStatus.Revoked"/> entries.
    /// </summary>
    Task<IReadOnlyList<WebhookSigningKey>> GetForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the signing key with the given identifier, or <c>null</c> if not found.</summary>
    Task<WebhookSigningKey?> FindByIdAsync(Guid keyId, CancellationToken cancellationToken = default);
}
