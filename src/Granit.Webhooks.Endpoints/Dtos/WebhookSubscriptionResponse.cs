using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Response representing a webhook subscription.
/// </summary>
/// <param name="Id">Unique subscription identifier.</param>
/// <param name="TargetUrl">HTTPS endpoint that receives webhook POSTs.</param>
/// <param name="EventType">Logical event type the subscription listens for.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="ConsecutiveFailureCount">Number of consecutive delivery failures since the last success.</param>
/// <param name="LastSuccessAt">UTC timestamp of the last successful delivery, if any.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="ModifiedAt">UTC last-modified timestamp, if any.</param>
/// <param name="SigningSecretHint">
/// Stripe-style masked preview of the active signing secret
/// (e.g. <c>whsec_b46a****************5182</c>) for admin UIs to display on the
/// detail/edit view without ever re-exposing the plaintext. Refreshed on every
/// signing-key rotation. <c>null</c> for subscriptions created before the hint
/// was introduced — UIs should fall back to a static placeholder in that case.
/// </param>
public sealed record WebhookSubscriptionResponse(
    Guid Id,
    string TargetUrl,
    string EventType,
    WebhookSubscriptionStatus Status,
    int ConsecutiveFailureCount,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? SigningSecretHint);
