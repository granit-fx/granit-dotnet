using System.Diagnostics;
using System.Text;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Granit.Core.MultiTenancy;
using Granit.Vault.Diagnostics;
using Granit.Vault.GoogleCloud.Diagnostics;
using Granit.Vault.GoogleCloud.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.GoogleCloud.Services;

/// <summary>
/// Transit encryption service using Google Cloud KMS symmetric encryption.
/// </summary>
internal sealed partial class CloudKmsTransitEncryptionService(
    KeyManagementServiceClient kmsClient,
    IOptions<GoogleCloudVaultOptions> options,
    VaultMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<CloudKmsTransitEncryptionService> logger) : ITransitEncryptionService
{
    private const string ProviderName = "googlecloud";
    private readonly CryptoKeyName _keyName = new(
        options.Value.ProjectId,
        options.Value.Location,
        options.Value.KeyRing,
        options.Value.CryptoKey);

    /// <inheritdoc />
    public async Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = VaultGoogleCloudActivitySource.Source.StartActivity(
            VaultGoogleCloudActivitySource.Operations.KmsEncrypt);
        activity?.SetTag(VaultGoogleCloudActivitySource.Tags.KeyName, keyName);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            EncryptResponse response = await kmsClient.EncryptAsync(
                _keyName,
                ByteString.CopyFrom(plaintextBytes),
                cancellationToken).ConfigureAwait(false);

            string ciphertext = Convert.ToBase64String(response.Ciphertext.ToByteArray());

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
        using Activity? activity = VaultGoogleCloudActivitySource.Source.StartActivity(
            VaultGoogleCloudActivitySource.Operations.KmsDecrypt);
        activity?.SetTag(VaultGoogleCloudActivitySource.Tags.KeyName, keyName);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);

            DecryptResponse response = await kmsClient.DecryptAsync(
                _keyName,
                ByteString.CopyFrom(ciphertextBytes),
                cancellationToken).ConfigureAwait(false);

            string result = Encoding.UTF8.GetString(response.Plaintext.ToByteArray());

            LogDecryptSuccess(keyName);
            metrics.RecordOperationCompleted(tenantId, "decrypt", ProviderName, "success");
            return result;
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
    /// Google Cloud KMS does not expose a server-side rewrap endpoint.
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
    /// <remarks>Google Cloud KMS ciphertext does not embed a key version — returns <c>null</c>.</remarks>
    public string? GetKeyVersion(string ciphertext) => null;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cloud KMS encrypt succeeded for key {KeyName}")]
    private partial void LogEncryptSuccess(string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cloud KMS decrypt succeeded for key {KeyName}")]
    private partial void LogDecryptSuccess(string keyName);
}
