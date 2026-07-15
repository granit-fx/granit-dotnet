namespace Granit.OpenIddict.Options;

/// <summary>
/// Configuration options for automatic signing key rotation.
/// </summary>
/// <remarks>
/// Bound from <c>appsettings.json</c> section <c>"OpenIddict:KeyRotation"</c>.
/// </remarks>
public sealed class GranitKeyRotationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenIddict:KeyRotation";

    /// <summary>
    /// Gets or sets a value indicating whether automatic key rotation is enabled.
    /// Default: <see langword="false"/> (ephemeral keys used in development).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of a signing key before rotation.
    /// Default: 90 days.
    /// </summary>
    public TimeSpan KeyLifetime { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets or sets the overlap period during which the new key is active alongside
    /// the retired key. Tokens signed with the retired key remain valid during this period.
    /// Default: 14 days (should be >= max token lifetime).
    /// </summary>
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Gets or sets how far in advance a new key is generated before the active key expires.
    /// Default: 7 days.
    /// </summary>
    public TimeSpan RotationLeadTime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets how often each running instance polls the key store for changes and reloads the
    /// signing/encryption credentials when the active key set has changed (e.g. after the rotation
    /// job minted a new key on another node). Default: 5 minutes. Must be well below
    /// <see cref="GracePeriod"/> so a rotated-in key is picked up before the retired key is revoked.
    /// </summary>
    public TimeSpan RefreshCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the RSA key size in bits.
    /// Default: 2048. Use 4096 for higher security requirements.
    /// </summary>
    public int RsaKeySize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the signing algorithm.
    /// Default: <c>"RS256"</c>. Use <c>"PS256"</c> (RSASSA-PSS) for FAPI 2.0 compliance.
    /// </summary>
    /// <remarks>
    /// When <see cref="GranitOpenIddictOptions.EnableFapi2Profile"/> is <see langword="true"/>,
    /// this is automatically overridden to <c>"PS256"</c> if still set to the default <c>"RS256"</c>.
    /// FAPI 2.0 §5.2.2 requires PS256 or ES256.
    /// </remarks>
    public string SigningAlgorithm { get; set; } = "RS256";
}
