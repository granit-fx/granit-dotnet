using Granit.Core.Domain;
using Granit.Core.Domain.ValueObjects;
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
public sealed class WebhookSubscription : AuditedAggregateRoot
{
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
        Guid? tenantId = null) => new()
        {
            Id = id,
            TargetUrl = targetUrl,
            EventType = eventType,
            SigningSecret = signingSecret,
            TenantId = tenantId,
            Status = WebhookSubscriptionStatus.Active,
        };

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
    /// Protected signing secret used to compute the <c>x-granit-signature</c> HMAC.
    /// The raw value is opaque — protected by <see cref="Abstractions.IWebhookSecretProtector"/>.
    /// Never store or log the plaintext secret. Maximum length: 1000 characters.
    /// </summary>
    public string SigningSecret { get; private set; } = string.Empty;

    /// <summary>
    /// Tenant this subscription belongs to.
    /// <c>null</c> indicates a global subscription that applies regardless of tenant context.
    /// </summary>
    public Guid? TenantId { get; private set; }

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
    /// </summary>
    internal void RecordSuccess(DateTimeOffset at)
    {
        LastSuccessAt = at;
        ConsecutiveFailureCount = 0;
    }

    /// <summary>
    /// Records a delivery failure.
    /// </summary>
    internal void RecordFailure()
    {
        ConsecutiveFailureCount++;
    }

    /// <summary>
    /// Suspends the subscription and emits a <see cref="WebhookSubscriptionSuspendedEvent"/> domain event.
    /// </summary>
    internal void Suspend(DateTimeOffset suspendedAt, string suspendedBy, string reason)
    {
        Status = WebhookSubscriptionStatus.Suspended;
        DeactivationReason = reason;
        SuspendedAt = suspendedAt;
        SuspendedBy = suspendedBy;
        AddDomainEvent(new WebhookSubscriptionSuspendedEvent(Id, reason));
    }

    /// <summary>
    /// Permanently deactivates the subscription and emits a <see cref="WebhookSubscriptionDeactivatedEvent"/> domain event.
    /// </summary>
    internal void Deactivate(string reason)
    {
        Status = WebhookSubscriptionStatus.Deactivated;
        DeactivationReason = reason;
        AddDomainEvent(new WebhookSubscriptionDeactivatedEvent(Id, reason));
    }

    /// <summary>
    /// Activates a suspended subscription. Clears suspension audit fields and resets failure counters.
    /// Emits a <see cref="WebhookSubscriptionActivatedEvent"/> domain event.
    /// </summary>
    internal void Activate()
    {
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
    internal void UpdateTargetUrl(HttpsUrl targetUrl)
    {
        TargetUrl = targetUrl;
    }

    /// <summary>
    /// Replaces the signing secret with a new protected value.
    /// </summary>
    internal void RotateSecret(string newProtectedSecret)
    {
        SigningSecret = newProtectedSecret;
    }
}
