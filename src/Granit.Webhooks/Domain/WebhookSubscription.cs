using Granit.DataProtection;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using Granit.Webhooks.Events;

namespace Granit.Webhooks.Domain;

/// <summary>
/// Represents an external subscriber registered to receive webhook events.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SigningSecret"/> field stores an opaque protected value whose format
/// is determined by the registered <see cref="Abstractions.IWebhookSecretProtector"/>.
/// Never log or expose this value. At delivery time, the handler calls
/// <see cref="Abstractions.IWebhookSecretProtector.UnprotectAsync"/> before signing.
/// </para>
/// <para>
/// ISO 27001 compliance: suspension actions are traced with <see cref="SuspendedAt"/>
/// and <see cref="SuspendedBy"/> (UserId only, never PII).
/// </para>
/// </remarks>
public sealed class WebhookSubscription : AuditedAggregateRoot, IMultiTenant
{
    private readonly List<WebhookSigningKey> _signingKeys = [];

    // Parameterless constructor required by EF Core materializer.
    private WebhookSubscription() { }

    /// <summary>
    /// Creates a new active <see cref="WebhookSubscription"/>.
    /// </summary>
    public static WebhookSubscription Create(
        Guid id,
        HttpsUrl targetUrl,
        string eventType,
        string signingSecret,
        Guid? tenantId = null)
    {
        var subscription = new WebhookSubscription
        {
            Id = id,
            TargetUrl = targetUrl,
            EventType = eventType,
#pragma warning disable CS0618 // SigningSecret is obsolete but still populated when callers
            SigningSecret = signingSecret,
#pragma warning restore CS0618 // hand in a legacy single-secret payload (back-compat).
            TenantId = tenantId,
            Status = WebhookSubscriptionStatus.Active,
        };

        subscription.AddDomainEvent(new WebhookSubscriptionCreatedEvent(id, eventType, targetUrl.Value));
        return subscription;
    }

    /// <summary>
    /// Creates a new active <see cref="WebhookSubscription"/> with an initial
    /// <see cref="WebhookSigningKey"/> rather than the legacy <see cref="SigningSecret"/> field.
    /// Preferred constructor for callers using the dual-key delivery model.
    /// </summary>
    public static WebhookSubscription CreateWithSigningKey(
        Guid id,
        HttpsUrl targetUrl,
        string eventType,
        Guid signingKeyId,
        string protectedSecret,
        DateTimeOffset createdAt,
        Guid? tenantId = null)
    {
        var subscription = new WebhookSubscription
        {
            Id = id,
            TargetUrl = targetUrl,
            EventType = eventType,
            TenantId = tenantId,
            Status = WebhookSubscriptionStatus.Active,
        };

        subscription._signingKeys.Add(WebhookSigningKey.Create(
            signingKeyId,
            id,
            protectedSecret,
            createdAt));

        subscription.AddDomainEvent(new WebhookSubscriptionCreatedEvent(id, eventType, targetUrl.Value));
        return subscription;
    }

    /// <summary>
    /// The HTTPS endpoint that receives webhook HTTP POST requests.
    /// Validated as an absolute HTTPS URL. Maximum length: 2048 characters.
    /// </summary>
    public HttpsUrl TargetUrl { get; private set; } = null!;

    /// <summary>
    /// Logical event type this subscription is registered for (e.g., <c>"document.uploaded"</c>).
    /// Maximum length: 200 characters.
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Legacy protected signing secret. Retained for backward-compatibility with subscriptions
    /// created before the <see cref="WebhookSigningKey"/> aggregate was introduced and never
    /// rotated since. Set to <c>null</c> on the first call to <see cref="RotateSigningKey"/>.
    /// </summary>
    /// <remarks>
    /// New subscriptions ship with an entry in <see cref="SigningKeys"/> and a <c>null</c>
    /// <see cref="SigningSecret"/>. Verification falls back to this field when no
    /// <see cref="WebhookSigningKey"/> matches.
    /// </remarks>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    [Obsolete("Use SigningKeys collection. Retained for backward compatibility — set to null on first rotation.")]
    public string? SigningSecret { get; private set; }

    /// <summary>
    /// Signing keys associated with this subscription, including
    /// <see cref="WebhookSigningKeyStatus.Active"/>, <see cref="WebhookSigningKeyStatus.Retired"/>
    /// (within grace period), and <see cref="WebhookSigningKeyStatus.Revoked"/> entries.
    /// </summary>
    public IReadOnlyList<WebhookSigningKey> SigningKeys => _signingKeys.AsReadOnly();

    /// <summary>
    /// Tenant this subscription belongs to.
    /// <c>null</c> indicates a global subscription that applies regardless of tenant context.
    /// </summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Current lifecycle status of the subscription.</summary>
    public WebhookSubscriptionStatus Status { get; private set; } = WebhookSubscriptionStatus.Active;

    /// <summary>
    /// Human-readable reason for suspension or deactivation.
    /// Set automatically on HTTP non-retriable errors. Maximum length: 500 characters.
    /// </summary>
    public string? DeactivationReason { get; private set; }

    /// <summary>
    /// Number of consecutive delivery failures since the last successful delivery.
    /// Reset to zero on success.
    /// </summary>
    public int ConsecutiveFailureCount { get; private set; }

    /// <summary>UTC timestamp of the last successful delivery. Null if never delivered.</summary>
    public DateTimeOffset? LastSuccessAt { get; private set; }

    /// <summary>UTC timestamp when the subscription was suspended. ISO 27001 audit field.</summary>
    public DateTimeOffset? SuspendedAt { get; private set; }

    /// <summary>
    /// UserId (not PII) of the operator who suspended the subscription, or the system identifier
    /// for automatic suspensions. ISO 27001 audit field. Maximum length: 450 characters.
    /// </summary>
    public string? SuspendedBy { get; private set; }

    /// <summary>
    /// Records a successful delivery. Resets failure counters.
    /// Emits a <see cref="WebhookDeliverySucceededEvent"/> domain event.
    /// </summary>
    internal void RecordSuccess(DateTimeOffset at)
    {
        LastSuccessAt = at;
        ConsecutiveFailureCount = 0;
        AddDomainEvent(new WebhookDeliverySucceededEvent(Id, at));
    }

    /// <summary>
    /// Records a delivery failure.
    /// Publishes a <see cref="WebhookDeliveryFailureThresholdExceededEto"/> integration event when <see cref="ConsecutiveFailureCount"/> reaches 5.
    /// </summary>
    internal void RecordFailure()
    {
        ConsecutiveFailureCount++;

        if (ConsecutiveFailureCount >= 5)
        {
            AddDistributedEvent(new WebhookDeliveryFailureThresholdExceededEto(
                Id, TargetUrl.Value, ConsecutiveFailureCount));
        }
    }

    /// <summary>
    /// Suspends the subscription and emits a <see cref="WebhookSubscriptionSuspendedEvent"/> domain event.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the subscription is not in <see cref="WebhookSubscriptionStatus.Active"/> status.
    /// </exception>
    internal void Suspend(DateTimeOffset suspendedAt, string suspendedBy, string reason)
    {
        if (Status != WebhookSubscriptionStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot suspend a subscription with status '{Status}'. Only 'Active' subscriptions can be suspended.");
        }

        Status = WebhookSubscriptionStatus.Suspended;
        DeactivationReason = reason;
        SuspendedAt = suspendedAt;
        SuspendedBy = suspendedBy;
        AddDomainEvent(new WebhookSubscriptionSuspendedEvent(Id, reason));
    }

    /// <summary>
    /// Permanently deactivates the subscription and emits a <see cref="WebhookSubscriptionDeactivatedEvent"/> domain event.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the subscription is already in <see cref="WebhookSubscriptionStatus.Deactivated"/> status.
    /// </exception>
    internal void Deactivate(string reason)
    {
        if (Status == WebhookSubscriptionStatus.Deactivated)
        {
            throw new InvalidOperationException("Subscription is already deactivated.");
        }

        Status = WebhookSubscriptionStatus.Deactivated;
        DeactivationReason = reason;
        AddDomainEvent(new WebhookSubscriptionDeactivatedEvent(Id, reason));
    }

    /// <summary>
    /// Activates a suspended subscription. Clears suspension audit fields and resets failure counters.
    /// Emits a <see cref="WebhookSubscriptionActivatedEvent"/> domain event.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the subscription is not in <see cref="WebhookSubscriptionStatus.Suspended"/> status.
    /// </exception>
    internal void Activate()
    {
        if (Status != WebhookSubscriptionStatus.Suspended)
        {
            throw new InvalidOperationException(
                $"Cannot activate a subscription with status '{Status}'. Only 'Suspended' subscriptions can be activated.");
        }

        Status = WebhookSubscriptionStatus.Active;
        SuspendedAt = null;
        SuspendedBy = null;
        DeactivationReason = null;
        ConsecutiveFailureCount = 0;
        AddDomainEvent(new WebhookSubscriptionActivatedEvent(Id));
    }

    /// <summary>
    /// Updates the target URL for webhook delivery.
    /// </summary>
    internal void UpdateTargetUrl(HttpsUrl targetUrl) =>
        TargetUrl = targetUrl;

    /// <summary>
    /// Replaces the legacy single signing secret with a new protected value.
    /// </summary>
    /// <remarks>
    /// Preserved for backward compatibility. New code should call
    /// <see cref="RotateSigningKey"/>, which uses the <see cref="WebhookSigningKey"/>
    /// aggregate and supports overlap rotation.
    /// </remarks>
    [Obsolete("Use RotateSigningKey for overlap-rotation semantics.")]
    internal void RotateSecret(string newProtectedSecret) =>
#pragma warning disable CS0618 // Intentionally writes to legacy field for back-compat.
        SigningSecret = newProtectedSecret;
#pragma warning restore CS0618

    /// <summary>
    /// Rotates the subscription's signing key using overlap-rotation semantics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The currently <see cref="WebhookSigningKeyStatus.Active"/> key (if any) is moved to
    /// <see cref="WebhookSigningKeyStatus.Retired"/> with an <see cref="WebhookSigningKey.ExpiresAt"/>
    /// set to <paramref name="now"/> + <paramref name="retiredKeyGracePeriod"/>. A new
    /// <see cref="WebhookSigningKeyStatus.Active"/> key is appended.
    /// </para>
    /// <para>
    /// On the first rotation following the upgrade from the legacy single-secret model,
    /// the legacy <see cref="SigningSecret"/> field is cleared. Note that legacy
    /// pre-upgrade secret material is intentionally NOT migrated into a
    /// <see cref="WebhookSigningKey"/> entity — the previous secret is dropped at
    /// rotation time, matching the behaviour of the legacy
    /// <see cref="RotateSecret(string)"/> method.
    /// </para>
    /// </remarks>
    /// <param name="newKeyId">Identifier for the new signing key.</param>
    /// <param name="newProtectedSecret">Opaque protected secret for the new key.</param>
    /// <param name="now">Current timestamp (provided by an <see cref="Granit.Timing.IClock"/>).</param>
    /// <param name="retiredKeyGracePeriod">Grace period during which the previously-active key remains accepted in verification.</param>
    /// <returns>The newly-created active key.</returns>
    internal WebhookSigningKey RotateSigningKey(
        Guid newKeyId,
        string newProtectedSecret,
        DateTimeOffset now,
        TimeSpan retiredKeyGracePeriod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newProtectedSecret);

        WebhookSigningKey? currentActive = _signingKeys
            .Find(k => k.Status == WebhookSigningKeyStatus.Active);

        currentActive?.Retire(now, retiredKeyGracePeriod);

        var newKey = WebhookSigningKey.Create(
            newKeyId,
            Id,
            newProtectedSecret,
            now);

        _signingKeys.Add(newKey);

#pragma warning disable CS0618 // Clears the legacy field — the new key is authoritative now.
        SigningSecret = null;
#pragma warning restore CS0618

        return newKey;
    }

    /// <summary>
    /// Revokes a specific signing key by id. The last <see cref="WebhookSigningKeyStatus.Active"/>
    /// key cannot be revoked — rotate first to introduce a new active key, then revoke.
    /// </summary>
    /// <returns><c>true</c> if a key was revoked; <c>false</c> if no key with the given id exists.</returns>
    internal bool RevokeSigningKey(Guid keyId, DateTimeOffset now)
    {
        WebhookSigningKey? key = _signingKeys.Find(k => k.Id == keyId);
        if (key is null)
        {
            return false;
        }

        if (key.Status == WebhookSigningKeyStatus.Active &&
            _signingKeys.Count(k => k.Status == WebhookSigningKeyStatus.Active) <= 1)
        {
            throw new InvalidOperationException(
                "Cannot revoke the last active signing key. Rotate first to introduce a new active key.");
        }

        key.Revoke(now);
        return true;
    }
}
