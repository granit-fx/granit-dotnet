using System.ComponentModel.DataAnnotations;

namespace Granit.Vault.Options;

/// <summary>
/// Options for <see cref="Services.SecretBackedMacService"/> — the portable
/// HMAC-SHA256 implementation that pulls a 32-byte key from <see cref="ISecretStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trust-boundary trade-off:</b> unlike the native Vault HMAC primitives, the key
/// material crosses the vault boundary into process memory. Acceptable for Azure
/// Key Vault Standard (no HSM available) or any host that prefers not to pay for a
/// native HMAC primitive. Memory is zeroed on dispose and on every cache refresh.
/// </para>
/// </remarks>
public sealed class SecretBackedMacOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:SecretBackedMac";

    /// <summary>
    /// Name of the secret holding the current key (base64-encoded, exactly 32 bytes
    /// decoded). Read at startup and on every refresh tick. Required.
    /// </summary>
    [Required]
    public string CurrentSecretName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the secret holding the previous key (base64-encoded, exactly 32 bytes
    /// decoded). When unset, no rolling-window verification is performed. Optional but
    /// strongly recommended to keep in-flight tags valid during rotation.
    /// </summary>
    public string? PreviousSecretName { get; set; }

    /// <summary>
    /// How often the background refresh hosted service re-reads the configured secrets
    /// from the vault. Default 5 minutes.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:30", "1.00:00:00")]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
}
