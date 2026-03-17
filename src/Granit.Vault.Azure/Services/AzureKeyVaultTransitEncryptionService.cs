using System.Text;
using Azure.Security.KeyVault.Keys.Cryptography;
using Granit.Vault.Azure.Diagnostics;
using Granit.Vault.Azure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.Services;

/// <summary>
/// Transit encryption service using Azure Key Vault cryptography.
/// </summary>
internal sealed partial class AzureKeyVaultTransitEncryptionService(
    CryptographyClient cryptographyClient,
    IOptions<AzureKeyVaultOptions> options,
    ILogger<AzureKeyVaultTransitEncryptionService> logger) : ITransitEncryptionService
{
    private readonly EncryptionAlgorithm _algorithm = MapAlgorithm(options.Value.EncryptionAlgorithm);

    /// <inheritdoc />
    public async Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvEncrypt);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, keyName);
        activity?.SetTag(VaultAzureActivitySource.Tags.VaultUri, options.Value.VaultUri);

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        EncryptResult result = await cryptographyClient
            .EncryptAsync(_algorithm, plaintextBytes, cancellationToken)
            .ConfigureAwait(false);

        string ciphertext = Convert.ToBase64String(result.Ciphertext);

        LogEncryptSuccess(keyName);
        return ciphertext;
    }

    /// <inheritdoc />
    public async Task<string> DecryptAsync(
        string keyName,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvDecrypt);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, keyName);
        activity?.SetTag(VaultAzureActivitySource.Tags.VaultUri, options.Value.VaultUri);

        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);

        DecryptResult result = await cryptographyClient
            .DecryptAsync(_algorithm, ciphertextBytes, cancellationToken)
            .ConfigureAwait(false);

        string plaintext = Encoding.UTF8.GetString(result.Plaintext);

        LogDecryptSuccess(keyName);
        return plaintext;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Azure Key Vault does not expose a server-side rewrap endpoint.
    /// This implementation falls back to decrypt-then-re-encrypt.
    /// </remarks>
    public async Task<string> RewrapAsync(
        string keyName,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        string plaintext = await DecryptAsync(keyName, ciphertext, cancellationToken).ConfigureAwait(false);
        return await EncryptAsync(keyName, plaintext, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>Azure Key Vault ciphertext does not embed a key version — returns <c>null</c>.</remarks>
    public string? GetKeyVersion(string ciphertext) => null;

    private static EncryptionAlgorithm MapAlgorithm(string algorithm) => algorithm switch
    {
        "RSA-OAEP" => EncryptionAlgorithm.RsaOaep,
        "RSA-OAEP-256" => EncryptionAlgorithm.RsaOaep256,
        "RSA1_5" => EncryptionAlgorithm.Rsa15,
        _ => throw new ArgumentException($"Unsupported encryption algorithm: {algorithm}", nameof(algorithm)),
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Azure Key Vault encrypt succeeded for key {KeyName}")]
    private partial void LogEncryptSuccess(string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Azure Key Vault decrypt succeeded for key {KeyName}")]
    private partial void LogDecryptSuccess(string keyName);
}
