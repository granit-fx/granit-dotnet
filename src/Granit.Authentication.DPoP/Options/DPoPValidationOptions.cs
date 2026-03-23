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
    /// Requires <see cref="IDistributedCache"/> to be registered.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool EnableReplayProtection { get; set; } = true;
}
