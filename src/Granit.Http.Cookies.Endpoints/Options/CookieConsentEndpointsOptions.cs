namespace Granit.Http.Cookies.Endpoints.Options;

/// <summary>
/// Configuration options for the cookie consent endpoints.
/// </summary>
public sealed class CookieConsentEndpointsOptions
{
    /// <summary>Base route prefix for the cookie consent endpoints. Default: "cookies".</summary>
    public string RoutePrefix { get; set; } = "cookies";

    /// <summary>OpenAPI tag name. Default: "Cookie Consent".</summary>
    public string TagName { get; set; } = "Cookie Consent";
}
