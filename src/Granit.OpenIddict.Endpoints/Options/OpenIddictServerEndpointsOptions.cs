namespace Granit.OpenIddict.Endpoints.Options;

/// <summary>
/// Options for the OIDC server protocol endpoints (<c>/connect/*</c>).
/// </summary>
public sealed class OpenIddictServerEndpointsOptions
{
    /// <summary>
    /// Path to redirect unauthenticated users during authorization.
    /// The cookie authentication handler appends a <c>ReturnUrl</c> query parameter.
    /// Default: <c>"/account/login"</c>.
    /// </summary>
    public string LoginPath { get; set; } = "/account/login";

    /// <summary>
    /// Fallback path to redirect after logout when no <c>post_logout_redirect_uri</c>
    /// is specified in the OIDC request. Default: <c>"/"</c>.
    /// </summary>
    public string PostLogoutRedirectPath { get; set; } = "/";
}
