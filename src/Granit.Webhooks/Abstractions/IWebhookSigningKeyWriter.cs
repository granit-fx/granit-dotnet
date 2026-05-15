namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Write operations for webhook signing keys (administrative actions).
/// </summary>
public interface IWebhookSigningKeyWriter
{
    /// <summary>
    /// Rotates the subscription's signing key using overlap-rotation semantics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The currently active key (if any) is moved to <c>Retired</c> and remains accepted in
    /// verification for <paramref name="retiredKeyGracePeriod"/>. A new active key is created
    /// and returned (plain-text — only once).
    /// </para>
    /// </remarks>
    /// <param name="subscriptionId">Target subscription identifier.</param>
    /// <param name="retiredKeyGracePeriod">
    /// Optional grace period overriding the configured default
    /// (<see cref="Options.WebhooksOptions.RetiredKeyGracePeriod"/>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Identifier of the new active key and its plain-text secret (returned once).</returns>
    Task<WebhookSigningKeyRotatedResult> RotateSigningKeyAsync(
        Guid subscriptionId,
        TimeSpan? retiredKeyGracePeriod = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a specific signing key. Verification will reject the key from this point on.
    /// </summary>
    /// <remarks>
    /// The last <see cref="Domain.WebhookSigningKeyStatus.Active"/> key cannot be revoked —
    /// rotate first to introduce a new active key.
    /// </remarks>
    /// <param name="subscriptionId">Owning subscription identifier.</param>
    /// <param name="keyId">Identifier of the key to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeSigningKeyAsync(
        Guid subscriptionId,
        Guid keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a rotation-due notification was emitted for the given key, by stamping
    /// <see cref="Domain.WebhookSigningKey.LastRotationNotificationAt"/>.
    /// </summary>
    /// <remarks>
    /// Used by the daily rotation scanner (FU-1b) to dedupe subsequent emissions to at most
    /// once per (key, calendar week).
    /// </remarks>
    /// <param name="subscriptionId">Owning subscription identifier.</param>
    /// <param name="keyId">Identifier of the key being notified.</param>
    /// <param name="notifiedAt">Timestamp recorded on the key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StampRotationNotificationAsync(
        Guid subscriptionId,
        Guid keyId,
        DateTimeOffset notifiedAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a successful <see cref="IWebhookSigningKeyWriter.RotateSigningKeyAsync"/> call.
/// </summary>
/// <param name="KeyId">Identifier of the newly-created active signing key.</param>
/// <param name="PlainSecret">Plain-text secret value (only returned once).</param>
public sealed record WebhookSigningKeyRotatedResult(Guid KeyId, string PlainSecret);
