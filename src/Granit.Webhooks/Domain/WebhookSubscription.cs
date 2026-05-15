using Granit.Domain;
using Granit.Domain.ValueObjects;
using Granit.Webhooks.Events;

namespace Granit.Webhooks.Domain;

/// <summary>
/// Represents an external subscriber registered to receive webhook events.
/// </summary>
/// <remarks>
/// Signing material lives in the <see cref="SigningKeys"/> collection (dual-key
/// model: one <see cref="WebhookSigningKeyStatus.Active"/> + zero-or-more
/// <see cref="WebhookSigningKeyStatus.Retired"/> within the grace period).
/// Never log or expose the protected secret value — at delivery time the handler
/// calls <see cref="Abstractions.IWebhookSecretProtector.UnprotectAsync"/> before signing.
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
    /// Creates a new active <see cref="WebhookSubscription"/> with an initial
    /// <see cref="WebhookSigningKeyStatus.Active"/> signing key.
    /// </summary>
    public static WebhookSubscription Create(
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
    /// Rotates the subscription's signing key using overlap-rotation semantics.
    /// </summary>
    /// <remarks>
    /// The currently <see cref="WebhookSigningKeyStatus.Active"/> key (if any) is moved to
    /// <see cref="WebhookSigningKeyStatus.Retired"/> with an <see cref="WebhookSigningKey.ExpiresAt"/>
    /// set to <paramref name="now"/> + <paramref name="retiredKeyGracePeriod"/>. A new
    /// <see cref="WebhookSigningKeyStatus.Active"/> key is appended.
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

        return newKey;
    }

    /// <summary>
    /// Stamps <see cref="WebhookSigningKey.LastRotationNotificationAt"/> on the matching key.
    /// Used by the rotation scanner (FU-1b) to dedupe subsequent emissions.
    /// </summary>
    /// <param name="keyId">Identifier of the key to stamp.</param>
    /// <param name="notifiedAt">Timestamp recorded on the key.</param>
    /// <returns><c>true</c> if a key was stamped; <c>false</c> if no key with the given id exists.</returns>
    internal bool StampRotationNotification(Guid keyId, DateTimeOffset notifiedAt)
    {
        WebhookSigningKey? key = _signingKeys.Find(k => k.Id == keyId);
        if (key is null)
        {
            return false;
        }

        key.StampRotationNotification(notifiedAt);
        return true;
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
