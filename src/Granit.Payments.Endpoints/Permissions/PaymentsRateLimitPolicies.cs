namespace Granit.Payments.Endpoints.Permissions;

/// <summary>
/// Rate limiting policy names for payment endpoints.
/// Configure limits in <c>RateLimiting:Policies</c> using these names as keys.
/// </summary>
/// <example>
/// <code>
/// "RateLimiting": {
///   "Policies": {
///     "payments-webhook": { "PermitLimit": 60, "Window": "00:01:00" }
///   }
/// }
/// </code>
/// </example>
public static class PaymentsRateLimitPolicies
{
    /// <summary>
    /// Policy for inbound payment provider webhooks (anonymous endpoint).
    /// Recommended: FixedWindow, 60 requests/minute per source IP.
    /// </summary>
    public const string Webhook = "payments-webhook";
}
