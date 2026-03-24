using Granit.Domain;
using Granit.Webhooks.Options;

namespace Granit.Webhooks.Domain;

/// <summary>
/// Immutable audit record of a single webhook delivery attempt.
/// </summary>
/// <remarks>
/// <para>
/// ISO 27001 compliance: this entity is INSERT-only. It must never be modified or deleted.
/// Do NOT use <see cref="AuditedEntity"/> or <see cref="FullAuditedEntity"/> — soft-delete
/// is explicitly prohibited to preserve the 3-year audit trail.
/// </para>
/// <para>
/// The <see cref="PayloadHash"/> stores the SHA-256 hex digest of the serialized
/// <c>WebhookEnvelope</c> body, allowing integrity verification without persisting
/// health data in clear text.
/// </para>
/// </remarks>
public sealed class WebhookDeliveryAttempt : Entity
{
    /// <summary>
    /// Unique identifier of this delivery attempt.
    /// Distinct from the <c>EventId</c> — one event may produce multiple attempts.
    /// </summary>
    public Guid DeliveryId { get; set; }

    /// <summary>Identifier of the <see cref="WebhookSubscription"/> targeted by this attempt.</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Tenant context at the time of delivery.
    /// <c>null</c> when multi-tenancy is not active.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Logical event type delivered (e.g., <c>"document.uploaded"</c>).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Target URL to which the HTTP POST was sent.</summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// HTTP response status code received from the target endpoint.
    /// <c>null</c> when the request timed out before receiving a response.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// SHA-256 hex digest of the serialized webhook envelope body.
    /// Allows integrity verification without storing health data in clear text (ISO 27001).
    /// Maximum length: 64 characters.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this delivery attempt occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Duration of the HTTP request in milliseconds. 0 if timed out before connection.</summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Error detail when the delivery failed.
    /// <c>null</c> on successful deliveries. Maximum length: 2000 characters.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether this attempt resulted in a 2xx HTTP response.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Serialized JSON body of the <see cref="Messages.WebhookEnvelope"/> sent to the subscriber.
    /// <c>null</c> when <see cref="WebhooksOptions.StorePayload"/> is <c>false</c> (default).
    /// </summary>
    /// <remarks>
    /// When stored, this field contains health data in clear text. Encryption at rest
    /// must be enabled on the database and RGPD validation by the DPO is required.
    /// </remarks>
    public string? Payload { get; set; }
}
