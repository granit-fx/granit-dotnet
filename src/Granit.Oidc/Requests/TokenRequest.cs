namespace Granit.Oidc.Requests;

/// <summary>
/// Abstract base for all OAuth 2.0 token endpoint requests.
/// </summary>
public abstract record TokenRequest
{
    /// <summary>
    /// The client identifier issued during registration.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Additional parameters to include in the token request.
    /// These are added after the standard parameters and will not override them.
    /// </summary>
    public Dictionary<string, string> AdditionalParameters { get; init; } = [];
}

/// <summary>
/// Token request for the authorization code grant type (RFC 6749 §4.1.3).
/// </summary>
public sealed record AuthorizationCodeTokenRequest : TokenRequest
{
    /// <summary>
    /// The authorization code received from the authorization server.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The redirect URI that was used in the authorization request.
    /// </summary>
    public required string RedirectUri { get; init; }

    /// <summary>
    /// The PKCE code verifier (RFC 7636).
    /// </summary>
    public required string CodeVerifier { get; init; }
}

/// <summary>
/// Token request for the refresh token grant type (RFC 6749 §6).
/// </summary>
public sealed record RefreshTokenRequest : TokenRequest
{
    /// <summary>
    /// The refresh token issued to the client.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Optional scope to request. If omitted, the original scope is used.
    /// </summary>
    public string? Scope { get; init; }
}

/// <summary>
/// Token request for the client credentials grant type (RFC 6749 §4.4).
/// </summary>
public sealed record ClientCredentialsTokenRequest : TokenRequest
{
    /// <summary>
    /// Optional scope to request.
    /// </summary>
    public string? Scope { get; init; }
}
