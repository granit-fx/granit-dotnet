using Granit.Core.Domain.ValueObjects;

namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Write operations for webhook subscriptions (administrative actions).
/// </summary>
public interface IWebhookSubscriptionWriter
{
    /// <summary>
    /// Creates a new active webhook subscription.
    /// </summary>
    /// <param name="targetUrl">HTTPS target URL for webhook delivery.</param>
    /// <param name="eventType">Logical event type (e.g., <c>"document.uploaded"</c>).</param>
    /// <param name="tenantId">Optional tenant scope. <c>null</c> for global subscriptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created subscription and its plain-text signing secret (returned once).</returns>
    Task<WebhookSubscriptionCreatedResult> CreateAsync(
        HttpsUrl targetUrl,
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the target URL of an existing subscription.
    /// </summary>
    Task UpdateTargetUrlAsync(Guid subscriptionId, HttpsUrl targetUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a suspended subscription, clearing failure counters and audit fields.
    /// </summary>
    Task ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends an active subscription.
    /// </summary>
    /// <param name="subscriptionId">Target subscription identifier.</param>
    /// <param name="suspendedBy">UserId of the operator (ISO 27001 audit trail).</param>
    /// <param name="reason">Human-readable suspension reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SuspendAsync(Guid subscriptionId, string suspendedBy, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deactivates a subscription.
    /// </summary>
    /// <param name="subscriptionId">Target subscription identifier.</param>
    /// <param name="reason">Human-readable deactivation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeactivateAsync(Guid subscriptionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes a subscription.
    /// </summary>
    Task DeleteAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the signing secret and returns the new plain-text secret (returned once).
    /// </summary>
    Task<string> RotateSecretAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}
