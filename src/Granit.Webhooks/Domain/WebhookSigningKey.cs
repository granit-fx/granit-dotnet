using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Webhooks.Domain;

/// <summary>
/// First-class signing key associated with a <see cref="WebhookSubscription"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per-subscription key history enabling overlap rotation: a previously-<see cref="WebhookSigningKeyStatus.Active"/>
/// key transitions to <see cref="WebhookSigningKeyStatus.Retired"/> with an
/// <see cref="ExpiresAt"/> in the (configurable) future and is still accepted in
/// verification until that grace period elapses, while the new key takes over signing.
/// </para>
/// <para>
/// The <see cref="ProtectedSecret"/> is opaque: its format is determined by the registered
/// <see cref="Abstractions.IWebhookSecretProtector"/>. Never log or expose this value.
/// </para>
/// <para>
/// ISO 27001 A.10.1 / A.8.24 — keys are explicitly versioned, expirable, and revocable.
/// </para>
/// </remarks>
public sealed class WebhookSigningKey : Entity
{
    // Parameterless constructor required by EF Core materializer.
    private WebhookSigningKey() { }

    /// <summary>
    /// Creates a new <see cref="WebhookSigningKey"/>.
    /// </summary>
    /// <param name="id">Unique identifier for the key.</param>
    /// <param name="subscriptionId">Owning <see cref="WebhookSubscription"/> identifier.</param>
    /// <param name="protectedSecret">Opaque protected secret (format defined by <see cref="Abstractions.IWebhookSecretProtector"/>).</param>
    /// <param name="createdAt">Creation timestamp (provided by an <see cref="Granit.Timing.IClock"/>).</param>
    /// <param name="status">Initial status — typically <see cref="WebhookSigningKeyStatus.Active"/>.</param>
    /// <param name="expiresAt">Optional expiration timestamp (relevant when <paramref name="status"/> is <see cref="WebhookSigningKeyStatus.Retired"/>).</param>
    public static WebhookSigningKey Create(
        Guid id,
        Guid subscriptionId,
        string protectedSecret,
        DateTimeOffset createdAt,
        WebhookSigningKeyStatus status = WebhookSigningKeyStatus.Active,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        return new WebhookSigningKey
        {
            Id = id,
            SubscriptionId = subscriptionId,
            ProtectedSecret = protectedSecret,
            CreatedAt = createdAt,
            Status = status,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Identifier of the owning <see cref="WebhookSubscription"/>.</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>
    /// Protected signing secret. Opaque — format is determined by <see cref="Abstractions.IWebhookSecretProtector"/>.
    /// Never log or expose this value. Maximum length: 1000 characters.
    /// </summary>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    public string ProtectedSecret { get; private set; } = string.Empty;

    /// <summary>UTC timestamp when this key was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// UTC timestamp when this key stops being accepted in verification.
    /// <c>null</c> for currently <see cref="WebhookSigningKeyStatus.Active"/> keys.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>UTC timestamp when this key was explicitly revoked by an operator.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// UTC timestamp of the most recent rotation-due notification emitted for this key.
    /// Populated by the rotation scanner (FU-1b) to dedupe notifications. <c>null</c> until
    /// the first notification is sent.
    /// </summary>
    public DateTimeOffset? LastRotationNotificationAt { get; private set; }

    /// <summary>Current lifecycle status — see <see cref="WebhookSigningKeyStatus"/>.</summary>
    public WebhookSigningKeyStatus Status { get; private set; } = WebhookSigningKeyStatus.Active;

    /// <summary>
    /// Indicates whether this key is still accepted in verification at <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="WebhookSigningKeyStatus.Active"/> keys are always accepted. <see cref="WebhookSigningKeyStatus.Retired"/>
    /// keys are accepted while <see cref="ExpiresAt"/> is in the future. <see cref="WebhookSigningKeyStatus.Revoked"/>
    /// keys are never accepted.
    /// </remarks>
    public bool IsAcceptableAt(DateTimeOffset now) => Status switch
    {
        WebhookSigningKeyStatus.Active => true,
        WebhookSigningKeyStatus.Retired => ExpiresAt is null || ExpiresAt > now,
        _ => false,
    };

    /// <summary>
    /// Transitions an <see cref="WebhookSigningKeyStatus.Active"/> key to
    /// <see cref="WebhookSigningKeyStatus.Retired"/>, setting an expiration in the future.
    /// </summary>
    internal void Retire(DateTimeOffset retiredAt, TimeSpan gracePeriod)
    {
        if (Status != WebhookSigningKeyStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot retire a signing key with status '{Status}'. Only 'Active' keys can be retired.");
        }

        Status = WebhookSigningKeyStatus.Retired;
        ExpiresAt = retiredAt + gracePeriod;
    }

    /// <summary>
    /// Marks the key as <see cref="WebhookSigningKeyStatus.Revoked"/>, blocking it from any future verification.
    /// </summary>
    internal void Revoke(DateTimeOffset revokedAt)
    {
        if (Status == WebhookSigningKeyStatus.Revoked)
        {
            throw new InvalidOperationException("Signing key is already revoked.");
        }

        Status = WebhookSigningKeyStatus.Revoked;
        RevokedAt = revokedAt;
    }

    /// <summary>
    /// Records the timestamp of the most recent rotation-due notification, used by the
    /// scanner (FU-1b) to dedupe emissions.
    /// </summary>
    internal void StampRotationNotification(DateTimeOffset notifiedAt) =>
        LastRotationNotificationAt = notifiedAt;
}
