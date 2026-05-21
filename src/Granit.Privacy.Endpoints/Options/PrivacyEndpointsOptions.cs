namespace Granit.Privacy.Endpoints.Options;

/// <summary>
/// Configuration options for the privacy GDPR endpoints.
/// </summary>
public sealed class PrivacyEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Privacy:Endpoints";

    /// <summary>
    /// Route prefix for all privacy endpoints.
    /// Default: <c>"privacy"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "privacy";

    /// <summary>
    /// OpenAPI tag name for grouping privacy endpoints.
    /// Default: <c>"Privacy"</c>.
    /// </summary>
    public string TagName { get; set; } = "Privacy";

    /// <summary>
    /// Rate limiting policy name applied to all Privacy endpoints.
    /// When <c>null</c> (default), no rate limiting is applied.
    /// Set to a policy name registered via <c>AddRateLimiter()</c> to protect against
    /// resource exhaustion through excessive export/deletion requests (OWASP API4).
    /// </summary>
    public string? RateLimitingPolicy { get; set; }
}
