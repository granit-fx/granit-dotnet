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
    /// Returns <c>true</c> when all required properties are set,
    /// indicating the OAuth2 scheme should be registered.
    /// </summary>
    internal bool IsConfigured =>
        !string.IsNullOrEmpty(AuthorizationUrl)
        && !string.IsNullOrEmpty(TokenUrl)
        && !string.IsNullOrEmpty(ClientId);
}
