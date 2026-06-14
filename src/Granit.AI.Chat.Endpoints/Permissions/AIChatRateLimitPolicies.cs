namespace Granit.AI.Chat.Endpoints.Permissions;

/// <summary>
/// Rate limiting policy names for the chat endpoints.
/// Configure limits in <c>RateLimiting:Policies</c> using these names as keys.
/// </summary>
/// <remarks>
/// The send endpoint drives the agentic loop — up to <c>MaxIterations</c> model round-trips plus
/// tool executions per request, i.e. real provider cost per call. Without a quota an authenticated
/// caller can run up unbounded spend ("denial of wallet", OWASP API4 / LLM10). The limiter is
/// always wired (the module depends on <c>GranitHttpRateLimitingModule</c>) but enforces nothing
/// until the host configures the policy below; operators are strongly advised to set one.
/// </remarks>
/// <example>
/// <code>
/// "RateLimiting": {
///   "Policies": {
///     "ai-chat-send": { "PermitLimit": 20, "Window": "00:01:00", "PartitionBy": "User" }
///   }
/// }
/// </code>
/// </example>
public static class AIChatRateLimitPolicies
{
    /// <summary>
    /// Policy applied to <c>POST /messages</c> — the agentic send. Bounds per-user model spend.
    /// Recommended: SlidingWindow, ~20 requests/min per user.
    /// </summary>
    public const string Send = "ai-chat-send";
}
