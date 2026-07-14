namespace Granit.Http.ApiDocumentation.Scalar.Options;

/// <summary>
/// Configuration options for the Scalar interactive API reference UI.
/// Document-generation settings (versions, title, OAuth2 endpoints, …) live in
/// <c>Granit.Http.ApiDocumentation</c> under <c>Http:ApiDocumentation</c>.
/// </summary>
public sealed class ScalarOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Http:ApiDocumentation:Scalar";

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
    /// Authorization policy applied to the OpenAPI JSON and Scalar UI endpoints.
    /// <list type="bullet">
    ///   <item><c>null</c> (default): no explicit policy — inherits the application's global behavior.</item>
    ///   <item>Empty string (<c>""</c>): explicitly allows anonymous access (<c>.AllowAnonymous()</c>).</item>
    ///   <item>Policy name (e.g. <c>"InternalDeveloper"</c>): requires authorization with that policy.</item>
    /// </list>
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// UI-side OAuth2 client configuration for Scalar's interactive Authorize button.
    /// Only effective when the OAuth2 endpoints are configured on the generation side
    /// (<c>Http:ApiDocumentation:OAuth2</c>).
    /// </summary>
    public ScalarOAuth2Options OAuth2 { get; set; } = new();
}
