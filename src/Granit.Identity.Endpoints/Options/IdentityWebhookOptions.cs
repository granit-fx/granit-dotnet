namespace Granit.Identity.Endpoints.Options;

/// <summary>
/// Configuration options for the identity webhook endpoint.
/// Bind to the <c>IdentityWebhook</c> configuration section.
/// </summary>
public sealed class IdentityWebhookOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "IdentityWebhook";

    /// <summary>
    /// Shared secret for HMAC-SHA256 signature validation.
    /// The provider must send the signature in the <see cref="SignatureHeaderName"/> header
    /// in the format <c>t=&lt;unix-seconds&gt;,v1=&lt;hex&gt;</c> where the HMAC is computed
    /// over <c>"&lt;unix-seconds&gt;.&lt;body&gt;"</c>. Leave empty to disable signature
    /// validation (not recommended for production).
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Name of the HTTP header containing the timestamped HMAC signature.
    /// Default: <c>"X-Webhook-Signature"</c>.
    /// </summary>
    public string SignatureHeaderName { get; set; } = "X-Webhook-Signature";

    /// <summary>
    /// Maximum acceptable skew between the timestamp embedded in the signature header
    /// and the receiver's clock. Webhooks whose <c>t=</c> component falls outside
    /// <c>now ± ReplayWindow</c> are rejected as either replays or clock-skew anomalies.
    /// Default: 5 minutes — matches Stripe's recommended tolerance.
    /// </summary>
    public TimeSpan ReplayWindow { get; set; } = TimeSpan.FromMinutes(5);
}
