namespace Granit.Caching.Vault.Options;

/// <summary>
/// Configuration for the Vault → <c>Cache:Encryption:Key</c> bridge.
/// Section <c>"Cache:Encryption:Vault"</c> in <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// When <see cref="SecretName"/> is set, <see cref="GranitCachingVaultModule"/> activates
/// and the AES key is fetched from <see cref="Granit.Vault.ISecretStore"/> at boot. Leave the section
/// empty (or unset <see cref="SecretName"/>) to fall back to the static
/// <c>Cache:Encryption:Key</c> value — typical Development setup.
/// </remarks>
public sealed class CacheEncryptionVaultOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache:Encryption:Vault";

    /// <summary>
    /// Provider-addressable secret name (interpretation depends on the active
    /// <see cref="Granit.Vault.ISecretStore"/> implementation — see
    /// <see cref="Granit.Vault.SecretRequest.Name"/> for examples).
    /// The secret payload MUST be the base64-encoded 32-byte AES-256 key.
    /// </summary>
    public string? SecretName { get; set; }

    /// <summary>
    /// Optional version pin. When <c>null</c>, the latest version is fetched.
    /// </summary>
    public string? SecretVersion { get; set; }
}
