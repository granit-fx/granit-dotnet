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

    /// <summary>
    /// Path to redirect when an OIDC application requires explicit user consent.
    /// The authorization handler appends a <c>returnUrl</c> query parameter containing
    /// the full <c>/connect/authorize</c> URL so the consent page can redirect back
    /// after the user grants or denies consent. Default: <c>"/consent"</c>.
    /// </summary>
    /// <remarks>
    /// The consent page is responsible for showing the requested scopes and client name,
    /// collecting the user's decision, and creating a permanent authorization via
    /// <c>POST /admin/oidc/authorizations</c> before redirecting back to the
    /// <c>returnUrl</c> (accept) or back with <c>error=access_denied</c> (deny).
    /// </remarks>
    public string ConsentPath { get; set; } = "/consent";

    /// <summary>
    /// Path to the device verification page for the OAuth 2.0 Device Authorization Grant
    /// (RFC 8628). The OpenIddict passthrough calls this page when the user navigates to
    /// <c>/connect/verify</c>. Default: <c>"/device"</c>.
    /// </summary>
    /// <remarks>
    /// Set to an empty string to disable the built-in redirect and handle
    /// <c>/connect/verify</c> entirely via custom middleware.
    /// </remarks>
    public string DeviceVerificationPath { get; set; } = "/device";
}
