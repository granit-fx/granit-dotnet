using System.Diagnostics;
using System.Text;
using Azure.Security.KeyVault.Keys.Cryptography;
using Granit.MultiTenancy;
using Granit.Vault.Azure.Diagnostics;
using Granit.Vault.Azure.Options;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.Services;

/// <summary>
/// Transit encryption service using Azure Key Vault cryptography.
/// </summary>
internal sealed partial class AzureKeyVaultTransitEncryptionService(
    CryptographyClient cryptographyClient,
    IOptions<AzureKeyVaultOptions> options,
    VaultMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<AzureKeyVaultTransitEncryptionService> logger) : ITransitEncryptionService
{
    private const string ProviderName = "azure";
    private readonly EncryptionAlgorithm _algorithm = MapAlgorithm(options.Value.EncryptionAlgorithm);

    /// <inheritdoc />
    public async Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvEncrypt);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, keyName);
        activity?.SetTag(VaultAzureActivitySource.Tags.VaultUri, options.Value.VaultUri);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            EncryptResult result = await cryptographyClient
                .EncryptAsync(_algorithm, plaintextBytes, cancellationToken)
                .ConfigureAwait(false);

            string ciphertext = Convert.ToBase64String(result.Ciphertext);

            LogEncryptSuccess(keyName);
            metrics.RecordOperationCompleted(tenantId, "encrypt", ProviderName, "success");
            return ciphertext;
        }
        catch
        {
            metrics.RecordOperationError(tenantId, "encrypt", ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordOperationDuration(tenantId, "encrypt", ProviderName, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public async Task<string> DecryptAsync(
        string keyName,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvDecrypt);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, keyName);
        activity?.SetTag(VaultAzureActivitySource.Tags.VaultUri, options.Value.VaultUri);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);

            DecryptResult result = await cryptographyClient
                .DecryptAsync(_algorithm, ciphertextBytes, cancellationToken)
                .ConfigureAwait(false);

            string plaintext = Encoding.UTF8.GetString(result.Plaintext);

            LogDecryptSuccess(keyName);
            metrics.RecordOperationCompleted(tenantId, "decrypt", ProviderName, "success");
            return plaintext;
        }
        catch
        {
            metrics.RecordOperationError(tenantId, "decrypt", ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordOperationDuration(tenantId, "decrypt", ProviderName, stopwatch.Elapsed);
        }
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
