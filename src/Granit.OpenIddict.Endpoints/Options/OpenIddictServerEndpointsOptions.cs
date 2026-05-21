namespace Granit.OpenIddict.Endpoints.Options;

/// <summary>
/// Options for the OIDC server protocol endpoints (<c>/connect/*</c>).
/// </summary>
public sealed class OpenIddictServerEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenIddict:Server:Endpoints";

    /// <summary>
    /// Default path to redirect unauthenticated users during authorization.
    /// The cookie authentication handler appends a <c>ReturnUrl</c> query parameter.
    /// Default: <c>"/login"</c>.
    /// </summary>
    public string LoginPath { get; set; } = "/login";

    /// <summary>
    /// Per-client login paths that override <see cref="LoginPath"/>.
    /// Key: OIDC <c>client_id</c>. Value: absolute or relative login URL.
    /// Useful in multi-frontend setups where each frontend has its own login page.
    /// </summary>
    public Dictionary<string, string> ClientLoginPaths { get; set; } = [];

    /// <summary>
    /// Fallback path to redirect after logout when no <c>post_logout_redirect_uri</c>
    /// is specified in the OIDC request. Default: <c>"/"</c>.
    /// </summary>
    public string PostLogoutRedirectPath { get; set; } = "/";
}
