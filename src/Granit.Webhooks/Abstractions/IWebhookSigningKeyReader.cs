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

    /// <summary>
    /// Returns signing keys whose <see cref="WebhookSigningKey.ExpiresAt"/> falls between
    /// <paramref name="now"/> (exclusive) and <paramref name="cutoff"/> (inclusive), and
    /// that are eligible for a rotation-due notification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Filters applied:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="WebhookSigningKey.Status"/> is <see cref="WebhookSigningKeyStatus.Active"/>
    ///         or <see cref="WebhookSigningKeyStatus.Retired"/> (revoked keys are excluded).</item>
    ///   <item><see cref="WebhookSigningKey.RevokedAt"/> is <c>null</c>.</item>
    ///   <item><see cref="WebhookSigningKey.ExpiresAt"/> is non-null, strictly greater than
    ///         <paramref name="now"/>, and less than or equal to <paramref name="cutoff"/>.</item>
    ///   <item><see cref="WebhookSigningKey.LastRotationNotificationAt"/> is <c>null</c> or
    ///         strictly less than <paramref name="notificationDedupeBefore"/>.</item>
    /// </list>
    /// <para>
    /// Used by the daily rotation scanner to dedupe emissions to at most once per
    /// (key, calendar week).
    /// </para>
    /// </remarks>
    /// <param name="now">Current timestamp — keys already expired are skipped.</param>
    /// <param name="cutoff">Upper bound for <see cref="WebhookSigningKey.ExpiresAt"/>
    /// (typically <c>now + RotationLeadTimeDays</c>).</param>
    /// <param name="notificationDedupeBefore">Keys notified at or after this timestamp are
    /// excluded (typically <c>now - 7 days</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<WebhookSigningKey>> GetExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset cutoff,
        DateTimeOffset notificationDedupeBefore,
        CancellationToken cancellationToken = default);
}
