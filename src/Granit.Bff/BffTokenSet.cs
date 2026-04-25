using Granit.Caching;

namespace Granit.Bff;

/// <summary>
/// Represents a set of OIDC tokens stored server-side for a BFF session.
/// Tokens never leave the server — the browser only holds a session cookie.
/// </summary>
/// <remarks>
/// SECURITY: <see cref="CacheEncryptedAttribute"/> forces AES-256-GCM encryption
/// of the cached payload regardless of the global
/// <see cref="Granit.Caching.Options.CachingOptions.EncryptValues"/> setting.
/// This protects access tokens, refresh tokens, ID tokens, and the DPoP
/// private key from any read-side breach of the L2 cache (Redis snapshot,
/// AOF leak, ACL bypass).
/// </remarks>
[CacheEncrypted]
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

    /// <summary>
    /// The user's subject identifier (<c>sub</c> claim from the ID token).
    /// Used to list/revoke all sessions for a user.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// The user-agent string from the login request. Used for session listing display.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Timestamp when the session was first created. Used to enforce
    /// <see cref="Options.GranitBffOptions.SessionAbsoluteMaxDuration"/>.
    /// </summary>
    public DateTimeOffset SessionCreatedAt { get; init; }

    /// <summary>
    /// Server-provided DPoP nonce for replay protection (RFC 9449 §8).
    /// Included in subsequent DPoP proof JWTs as the <c>nonce</c> claim.
    /// Updated when the server returns a new <c>DPoP-Nonce</c> header.
    /// </summary>
    public string? DPoPNonce { get; init; }
}
#pragma warning restore GRSEC003
