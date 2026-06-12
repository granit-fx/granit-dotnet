namespace Granit.Identity.Endpoints.Options;

/// <summary>
/// Configuration options for identity user cache endpoints.
/// </summary>
public sealed class IdentityEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Endpoints";

    /// <summary>
    /// Route prefix for all identity user cache endpoints.
    /// Default: <c>"identity/users"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "identity/users";

    /// <summary>
    /// OpenAPI tag name for grouping identity endpoints in Swagger UI.
    /// Default: <c>"Identity - User Cache"</c>.
    /// </summary>
    public string TagName { get; set; } = "Identity - User Cache";

    /// <summary>
    /// OpenAPI tag name for the identity-provider webhook receiver endpoint.
    /// Default: <c>"Identity - Webhook"</c>.
    /// </summary>
    public string WebhookTagName { get; set; } = "Identity - Webhook";

    /// <summary>
    /// Gets or sets whether session and device endpoints expose the raw client IP address. When
    /// <see langword="false"/> (default), the IP is masked (host portion zeroed) before being returned — GDPR
    /// data minimisation. These endpoints are permission-gated, so a deployment may opt in to raw IPs.
    /// </summary>
    public bool ExposeRawIpAddress { get; set; }
}
