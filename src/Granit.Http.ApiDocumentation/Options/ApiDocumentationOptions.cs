namespace Granit.Http.ApiDocumentation.Options;

/// <summary>Configuration options for Granit OpenAPI documentation.</summary>
public sealed class ApiDocumentationOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Http:ApiDocumentation";

    /// <summary>
    /// Major API version numbers to document. Each entry generates a distinct OpenAPI document
    /// (e.g., <c>/openapi/v1.json</c>, <c>/openapi/v2.json</c>).
    /// Default: <c>[1]</c>.
    /// </summary>
    public IList<int> MajorVersions { get; set; } = [1];

    /// <summary>Title of the API shown in the Scalar UI and OpenAPI document. Default: <c>"API"</c>.</summary>
    public string Title { get; set; } = "API";

    /// <summary>
    /// Description shown in the OpenAPI document info block. Supports Markdown.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>Contact email shown in the OpenAPI document info block.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// URL to a logo image displayed in the Scalar UI sidebar and OpenAPI document.
    /// Can be an absolute URL or a path served by the application (e.g. <c>"/logo.svg"</c>).
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// URL or path to a favicon for the Scalar documentation page.
    /// Can be an absolute URL or a path served by the application (e.g. <c>"/favicon.svg"</c>).
    /// </summary>
    public string? FaviconUrl { get; set; }

    /// <summary>
    /// When <c>true</c>, exposes the Scalar UI and OpenAPI JSON endpoints even in Production.
    /// Default: <c>false</c> — UI is enabled in Development only.
    /// </summary>
    public bool EnableInProduction { get; set; }

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
    /// Authorization policy applied to the OpenAPI JSON and Scalar UI endpoints.
    /// <list type="bullet">
    ///   <item><c>null</c> (default): no explicit policy — inherits the application's global behavior.</item>
    ///   <item>Empty string (<c>""</c>): explicitly allows anonymous access (<c>.AllowAnonymous()</c>).</item>
    ///   <item>Policy name (e.g. <c>"InternalDeveloper"</c>): requires authorization with that policy.</item>
    /// </list>
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// OAuth2 configuration for the OpenAPI document and Scalar UI.
    /// When configured, replaces the HTTP Bearer scheme with an OAuth2 Authorization Code flow.
    /// </summary>
    public OAuth2Options OAuth2 { get; set; } = new();
}
