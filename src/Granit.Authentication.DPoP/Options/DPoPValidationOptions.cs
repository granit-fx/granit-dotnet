namespace Granit.Authentication.DPoP.Options;

/// <summary>
/// Configuration options for DPoP proof validation on resource servers.
/// </summary>
public sealed class DPoPValidationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication:DPoP";

    /// <summary>
    /// Gets or sets whether DPoP proof-of-possession is required for all authenticated requests.
    /// When <see langword="true"/>, requests using <c>Authorization: Bearer</c> without a
    /// <c>DPoP</c> proof header are rejected with 401.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool RequireDPoP { get; set; }

    /// <summary>
    /// Gets or sets the allowed signing algorithms for DPoP proof JWTs.
    /// Default: ES256, PS256.
    /// </summary>
    public string[] AllowedAlgorithms { get; set; } = ["ES256", "PS256"];

    /// <summary>
    /// Gets or sets the maximum allowed clock skew for proof expiration validation.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum proof lifetime (exp - iat). Proofs older than this are rejected.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan MaxProofLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether jti replay protection is enabled.
    /// Requires <c>IFusionCache</c> to be registered (via <c>GranitCachingModule</c>).
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool EnableReplayProtection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether server-issued nonces are required in DPoP proofs (RFC 9449 §8).
    /// When enabled, the server generates a nonce per response via the <c>DPoP-Nonce</c> header.
    /// Clients must include this nonce in the <c>nonce</c> claim of subsequent proofs.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool RequireNonce { get; set; }

    /// <summary>
    /// Gets or sets whether the access token must contain a <c>cnf.jkt</c> claim
    /// matching the DPoP proof's JWK thumbprint (RFC 9449 §4.3).
    /// When <see langword="true"/>, DPoP proofs are rejected if the access token
    /// does not include sender-constraint confirmation.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool RequireTokenBinding { get; set; }

    /// <summary>
    /// Gets or sets the minimum RSA key size (in bits) accepted in DPoP proof JWKs.
    /// Keys smaller than this are rejected during signature verification.
    /// Default: 2048 (NIST SP 800-57 recommendation).
    /// </summary>
    public int MinimumRsaKeySize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets request path prefixes the resource-side validation middleware skips
    /// entirely (case-insensitive segment match). Requests under these prefixes pass
    /// straight through without proof validation.
    /// <para>
    /// This matters when the OpenID Connect authorization server is co-located with the
    /// resource server (a monolith). The DPoP proof presented at the token endpoint
    /// (<c>/connect/token</c>) is validated server-side by the OIDC pipeline, which records
    /// the proof's <c>jti</c> for replay protection. If this middleware also validated the
    /// same proof, it would record the <c>jti</c> first, so the server handler would then
    /// see a duplicate <c>jti</c> → "proof replay detected" → the token exchange fails.
    /// Excluding the OIDC endpoints avoids the double validation. On a standalone resource
    /// server these paths do not exist, so the default is harmless.
    /// </para>
    /// Default: <c>["/connect"]</c> (the Granit OpenIddict server endpoint prefix).
    /// </summary>
    public string[] ExcludedPathPrefixes { get; set; } = ["/connect"];
}
