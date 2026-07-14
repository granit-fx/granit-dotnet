namespace Granit.Http.ApiDocumentation.Options;

/// <summary>Configuration options for Granit OpenAPI documentation and API versioning.</summary>
/// <remarks>
/// UI-only settings (favicon, production exposure of the interactive reference,
/// authorization policy, OAuth2 client) live in the optional
/// <c>Granit.Http.ApiDocumentation.Scalar</c> companion package under
/// <c>Http:ApiDocumentation:Scalar</c>.
/// </remarks>
public sealed class ApiDocumentationOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Http:ApiDocumentation";

    /// <summary>
    /// Major API version numbers to document. Each entry generates a distinct OpenAPI document
    /// (e.g., <c>/openapi/v1.json</c>, <c>/openapi/v2.json</c>) and is a routable
    /// <c>v{version:apiVersion}</c> URL segment. Single source of truth for API versions.
    /// Default: <c>[1]</c>.
    /// </summary>
    public IList<int> MajorVersions { get; set; } = [1];

    /// <summary>
    /// Default API major version assumed when the client does not specify one.
    /// Must be one of <see cref="MajorVersions"/> (validated at startup).
    /// Default: <c>1</c>.
    /// </summary>
    public int DefaultMajorVersion { get; set; } = 1;

    /// <summary>
    /// When <c>true</c>, adds <c>api-supported-versions</c> and <c>api-deprecated-versions</c>
    /// response headers on every response. Useful for audit trails and client deprecation notices.
    /// Default: <c>true</c>.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;

    /// <summary>Title of the API shown in the OpenAPI document info block. Default: <c>"API"</c>.</summary>
    public string Title { get; set; } = "API";

    /// <summary>
    /// Description shown in the OpenAPI document info block. Supports Markdown.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>Contact email shown in the OpenAPI document info block.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// URL to a logo image emitted as the <c>x-logo</c> extension in the OpenAPI document.
    /// Can be an absolute URL or a path served by the application (e.g. <c>"/logo.svg"</c>).
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// When <c>true</c>, documents a required tenant header on all endpoints except those
    /// decorated with <c>[AllowAnonymousTenant]</c>.
    /// Default: <c>false</c>.
    /// </summary>
    public bool EnableTenantHeader { get; set; }

    /// <summary>
    /// Name of the HTTP header that carries the tenant identifier.
    /// Default: <c>"X-Tenant-Id"</c>.
    /// </summary>
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// OAuth2 configuration for the OpenAPI document.
    /// When configured, replaces the HTTP Bearer scheme with an OAuth2 Authorization Code flow.
    /// </summary>
    public OAuth2Options OAuth2 { get; set; } = new();
}
