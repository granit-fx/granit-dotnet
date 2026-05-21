namespace Granit.Http.ApiDocumentation.Options;

/// <summary>
/// OAuth2 configuration for the OpenAPI document and Scalar UI.
/// When fully configured, replaces the HTTP Bearer scheme with an OAuth2
/// Authorization Code flow, enabling interactive authentication in Scalar.
/// </summary>
public sealed class OAuth2Options
{
    /// <summary>OAuth2 authorization endpoint URL (e.g. Keycloak <c>{Authority}/protocol/openid-connect/auth</c>).</summary>
    public string? AuthorizationUrl { get; set; }

    /// <summary>OAuth2 token endpoint URL (e.g. Keycloak <c>{Authority}/protocol/openid-connect/token</c>).</summary>
    public string? TokenUrl { get; set; }

    /// <summary>
    /// OAuth2 client ID — the <strong>public</strong> (frontend) client, not the backend confidential client.
    /// Must support PKCE (Authorization Code + S256).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>Enable PKCE (Proof Key for Code Exchange) with S256. Default: <c>true</c>.</summary>
    public bool EnablePkce { get; set; } = true;

    /// <summary>OAuth2 scopes to request. Default: <c>["openid"]</c>.</summary>
    public IList<string> Scopes { get; set; } = ["openid"];

    /// <summary>
    /// Absolute redirect URI passed to the Scalar OAuth2 flow. Workaround for
    /// <c>Scalar.AspNetCore</c> 2.12.40+ where the default redirect URI changed
    /// and breaks Authorization Code popups (scalar/scalar#8165, #8187): the
    /// popup re-opens same-origin but on a URL Scalar no longer recognises, so
    /// the auth code is never piped back and the user sees
    /// "Window was closed without granting authorization." Typically set to
    /// the absolute URL of the Scalar UI (e.g. <c>http://localhost:5000/scalar</c>).
    /// When <c>null</c>, Scalar's (currently broken) default is used — leave it
    /// unset only when you trust the upstream default again.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Returns <c>true</c> when all required properties are set,
    /// indicating the OAuth2 scheme should be registered.
    /// </summary>
    internal bool IsConfigured =>
        !string.IsNullOrEmpty(AuthorizationUrl)
        && !string.IsNullOrEmpty(TokenUrl)
        && !string.IsNullOrEmpty(ClientId);
}
