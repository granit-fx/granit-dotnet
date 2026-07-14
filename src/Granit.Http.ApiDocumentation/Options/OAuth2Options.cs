namespace Granit.Http.ApiDocumentation.Options;

/// <summary>
/// OAuth2 configuration for the OpenAPI document. When fully configured, replaces
/// the HTTP Bearer scheme with an OAuth2 Authorization Code flow in the generated
/// document. UI-side settings (client id, PKCE, redirect URI) live in the
/// <c>Granit.Http.ApiDocumentation.Scalar</c> companion package.
/// </summary>
public sealed class OAuth2Options
{
    /// <summary>OAuth2 authorization endpoint URL (e.g. Keycloak <c>{Authority}/protocol/openid-connect/auth</c>).</summary>
    public string? AuthorizationUrl { get; set; }

    /// <summary>OAuth2 token endpoint URL (e.g. Keycloak <c>{Authority}/protocol/openid-connect/token</c>).</summary>
    public string? TokenUrl { get; set; }

    /// <summary>OAuth2 scopes to request. Default: <c>["openid"]</c>.</summary>
    public IList<string> Scopes { get; set; } = ["openid"];

    /// <summary>
    /// Returns <c>true</c> when both endpoint URLs are set,
    /// indicating the OAuth2 scheme should be documented.
    /// </summary>
    internal bool IsConfigured =>
        !string.IsNullOrEmpty(AuthorizationUrl)
        && !string.IsNullOrEmpty(TokenUrl);
}
