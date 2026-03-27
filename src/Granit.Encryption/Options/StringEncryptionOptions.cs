namespace Granit.Encryption.Options;

/// <summary>
/// Configuration options for the string encryption service.
/// </summary>
public sealed class StringEncryptionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Encryption";

    /// <summary>Name of the local AES provider.</summary>
    public const string AesProviderName = "Aes";

    /// <summary>Name of the Vault Transit provider.</summary>
    public const string VaultProviderName = "Vault";

    /// <summary>
    /// Passphrase used by the AES provider to derive the encryption key.
    /// MUST be supplied via Vault config provider — never hardcoded or committed.
    /// </summary>
    public string PassPhrase { get; set; } = string.Empty;

    /// <summary>AES key size in bits (256 by default = AES-256). Valid values: 128, 192, 256.</summary>
    public int KeySize { get; set; } = 256;

    /// <summary>
    /// When <c>true</c>, allows an ephemeral random passphrase for development/test environments.
    /// When <c>false</c> (default), a missing <see cref="PassPhrase"/> causes a startup failure.
    /// NEVER enable in production — ephemeral passphrases cause data loss on pod restart.
    /// </summary>
    public bool AllowEphemeralPassPhrase { get; set; }

    /// <summary>
    /// Active provider name. Values: "Aes" (default) or "Vault".
    /// </summary>
    public string ProviderName { get; set; } = AesProviderName;

    /// <summary>
    /// Vault Transit key name used by VaultStringEncryptionProvider.
    /// Ignored when ProviderName != "Vault".
    /// </summary>
    public string VaultKeyName { get; set; } = "string-encryption";
}
