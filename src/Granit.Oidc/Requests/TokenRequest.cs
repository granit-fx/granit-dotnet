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

/// <summary>
/// Token request for the OAuth 2.0 Token Exchange grant type (RFC 8693 §2.1).
/// Used for act-on-behalf-of flows where a service exchanges an inbound user
/// access token for a downstream-scoped access token with a narrowed
/// <c>audience</c> and reduced <c>scope</c>. Preferred over bearer-token
/// propagation because the resulting token cannot be replayed against the
/// original audience.
/// </summary>
public sealed record TokenExchangeTokenRequest : TokenRequest
{
    /// <summary>
    /// The token whose identity is being exchanged — typically the inbound
    /// user access token (RFC 8693 §2.1 <c>subject_token</c>).
    /// </summary>
    public required string SubjectToken { get; init; }

    /// <summary>
    /// RFC 8693 token type URI for the subject token — usually
    /// <see cref="OidcConstants.TokenTypeIdentifiers.AccessToken"/>.
    /// </summary>
    public required string SubjectTokenType { get; init; }

    /// <summary>
    /// Requested audience — constrains the <c>aud</c> claim of the issued
    /// token to the downstream API. Mandatory for a safe exchange: without it
    /// the issued token may be a full equivalent of the caller's, defeating
    /// the purpose.
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Optional scope(s) to request. When omitted the IdP may apply its own
    /// scope-narrowing policy.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Optional <c>resource</c> parameter (RFC 8693 §2.1 / RFC 8707).
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Optional requested token type. Defaults to
    /// <see cref="OidcConstants.TokenTypeIdentifiers.AccessToken"/> at the IdP.
    /// </summary>
    public string? RequestedTokenType { get; init; }

    /// <summary>
    /// Optional actor token for delegation chaining (RFC 8693 §1.2 —
    /// <c>actor_token</c>). Identifies the caller acting on behalf of the
    /// subject.
    /// </summary>
    public string? ActorToken { get; init; }

    /// <summary>
    /// Optional actor token type URI.
    /// </summary>
    public string? ActorTokenType { get; init; }
}
