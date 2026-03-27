namespace Granit.AI.Options;

/// <summary>
/// Per-tenant AI usage quota configuration.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Quota</c> configuration section.
/// When <see cref="MaxRequestsPerTenantPerHour"/> is 0 (default), no limit is enforced.
/// </remarks>
public sealed class AIQuotaOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AI:Quota";

    /// <summary>
    /// Maximum AI requests per tenant per rolling hour. 0 = unlimited (default).
    /// </summary>
    /// <example>
    /// <code>
    /// "AI": {
    ///   "Quota": {
    ///     "MaxRequestsPerTenantPerHour": 500
    ///   }
    /// }
    /// </code>
    /// </example>
    public int MaxRequestsPerTenantPerHour { get; set; }

    /// <summary>
    /// Behavior when a tenant exceeds the quota.
    /// </summary>
    public AIQuotaExceededBehavior ExceededBehavior { get; set; } = AIQuotaExceededBehavior.Skip;
}

/// <summary>
/// Controls what happens when an AI quota is exceeded.
/// </summary>
public enum AIQuotaExceededBehavior
{
    /// <summary>
    /// Skip the AI call gracefully (return a default/unknown result).
    /// The calling operation (e.g. blob upload) succeeds without AI enrichment.
    /// </summary>
    Skip,

    /// <summary>
    /// Reject the operation with an error. Use for strict enforcement.
    /// </summary>
    Reject,
}
