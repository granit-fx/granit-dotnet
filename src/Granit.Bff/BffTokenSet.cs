namespace Granit.Bff;

/// <summary>
/// Represents a set of OIDC tokens stored server-side for a BFF session.
/// Tokens never leave the server — the browser only holds a session cookie.
/// </summary>
#pragma warning disable GRSEC003 // Record contains token properties — stored server-side only
public sealed record BffTokenSet(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// DPoP private key as JWK JSON. Stored server-side only, never exposed to the browser.
    /// When present, the BFF uses DPoP token binding (RFC 9449) for this session.
    /// </summary>
    public string? DPoPPrivateKeyJwk { get; init; }
}
#pragma warning restore GRSEC003
