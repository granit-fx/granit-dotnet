namespace Granit.Vault;

/// <summary>
/// Transit encryption/decryption service for protecting sensitive data at rest.
/// Implemented by provider-specific packages (HashiCorp Vault Transit, Azure Key Vault, AWS KMS).
/// </summary>
public interface ITransitEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using the configured transit engine.
    /// </summary>
    /// <param name="keyName">Logical key name (e.g. "sensitive-data").</param>
    /// <param name="plaintext">Plaintext to encrypt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-specific ciphertext (e.g. "vault:v1:..." for HashiCorp, Base64 for Azure/AWS).</returns>
    Task<string> EncryptAsync(string keyName, string plaintext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts ciphertext using the configured transit engine.
    /// </summary>
    /// <param name="keyName">Logical key name (e.g. "sensitive-data").</param>
    /// <param name="ciphertext">Provider-specific ciphertext.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decrypted plaintext.</returns>
    Task<string> DecryptAsync(string keyName, string ciphertext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-encrypts existing ciphertext to the latest key version without exposing plaintext.
    /// For providers that support server-side key rotation (e.g. HashiCorp Vault Transit),
    /// this is done entirely server-side. For other providers, it falls back to
    /// decrypt-then-re-encrypt.
    /// </summary>
    /// <param name="keyName">Logical key name.</param>
    /// <param name="ciphertext">Existing ciphertext to re-encrypt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New ciphertext encrypted with the latest key version.</returns>
    Task<string> RewrapAsync(string keyName, string ciphertext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the key version embedded in a ciphertext, if the provider supports
    /// versioned ciphertext (e.g. HashiCorp Vault Transit format <c>vault:v{N}:...</c>).
    /// </summary>
    /// <param name="ciphertext">Provider-specific ciphertext.</param>
    /// <returns>
    /// Version string (e.g. <c>"v1"</c>) for versioned providers;
    /// <c>null</c> for providers whose ciphertext does not embed a version.
    /// </returns>
    string? GetKeyVersion(string ciphertext);
}
