using System.ComponentModel.DataAnnotations;

namespace Granit.Vault.GoogleCloud.Options;

/// <summary>
/// Configuration for the Google Cloud KMS-backed <see cref="ITransitMacService"/>.
/// </summary>
public sealed class GoogleCloudKmsMacOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:GoogleCloud:Mac";

    /// <summary>
    /// CryptoKey name (without the <c>projects/.../keyRings/.../cryptoKeys/</c> prefix —
    /// the prefix is resolved from <see cref="GoogleCloudVaultOptions"/>). The key
    /// purpose must be <c>MAC</c> and the algorithm <c>HMAC_SHA256</c>. Required.
    /// </summary>
    [Required]
    public string CryptoKeyId { get; set; } = string.Empty;
}
