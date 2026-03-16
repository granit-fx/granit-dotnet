namespace Granit.Caching.Options;

/// <summary>
/// AES encryption options for the cache. Section <c>"Cache:Encryption"</c> in <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The AES key must be provided exclusively via Vault or secure environment variables.
/// Never commit the key in plaintext to the repository.
/// </remarks>
public sealed class CacheEncryptionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache:Encryption";

    /// <summary>
    /// AES-256 key encoded in base64 (256 bits = 32 bytes).
    /// Generation example: <c>Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))</c>.
    /// In production: provided by HashiCorp Vault or ESO (External Secrets Operator).
    /// </summary>
    public string? Key { get; set; }
}
