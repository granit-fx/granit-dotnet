namespace Granit.Http.SecurityHeaders.Endpoints.Options;

/// <summary>
/// Options for the CSP audit endpoint.
/// </summary>
public sealed class SecurityHeadersEndpointsOptions
{
    /// <summary>
    /// Route prefix for the audit endpoint. The endpoint is mounted at
    /// <c>/{prefix}/csp</c>. Default: <c>"security-headers"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "security-headers";

    /// <summary>
    /// OpenAPI tag name for the audit endpoint.
    /// Default: <c>"Security Headers"</c>.
    /// </summary>
    public string TagName { get; set; } = "Security Headers";
}
