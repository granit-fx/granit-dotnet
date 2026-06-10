namespace Granit.OpenIddict.Endpoints.Options;

/// <summary>
/// Options for configuring OpenIddict account endpoints.
/// </summary>
public sealed class OpenIddictEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenIddict:Endpoints";

    /// <summary>Route prefix for account self-service endpoints. Default: <c>"account"</c>.</summary>
    public string AccountRoutePrefix { get; set; } = "account";

    /// <summary>Route prefix for admin management endpoints. Default: <c>"admin"</c>.</summary>
    public string AdminRoutePrefix { get; set; } = "admin";

    /// <summary>OpenAPI tag name for admin OIDC management endpoints. Default: <c>"OIDC - Admin"</c>.</summary>
    public string AdminTagName { get; set; } = "OIDC - Admin";

    /// <summary>Route prefix for authenticated (non-admin) OIDC endpoints used by the consent page. Default: <c>"oidc"</c>.</summary>
    public string OidcRoutePrefix { get; set; } = "oidc";

    /// <summary>OpenAPI tag name for OIDC consent endpoints. Default: <c>"OIDC"</c>.</summary>
    public string OidcTagName { get; set; } = "OIDC";
}
