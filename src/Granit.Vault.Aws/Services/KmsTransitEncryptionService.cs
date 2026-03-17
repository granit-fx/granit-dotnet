using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Granit.Vault.Aws.Diagnostics;
using Granit.Vault.Aws.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Aws.Services;

/// <summary>
/// Transit encryption service using AWS KMS symmetric encryption.
/// </summary>
internal sealed partial class KmsTransitEncryptionService(
    IAmazonKeyManagementService kmsClient,
    IOptions<AwsVaultOptions> options,
    ILogger<KmsTransitEncryptionService> logger) : ITransitEncryptionService
{
    private readonly string _keyId = options.Value.KmsKeyId;

    /// <inheritdoc />
    public async Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity = VaultAwsActivitySource.Source.StartActivity(
            VaultAwsActivitySource.Operations.KmsEncrypt);
        activity?.SetTag(VaultAwsActivitySource.Tags.KeyName, keyName);

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        EncryptRequest request = new()
        {
            KeyId = _keyId,
            Plaintext = new MemoryStream(plaintextBytes),
        };

        EncryptResponse response = await kmsClient.EncryptAsync(request, cancellationToken)
            .ConfigureAwait(false);

        string ciphertext = Convert.ToBase64String(response.CiphertextBlob.ToArray());

        LogEncryptSuccess(keyName);
        return ciphertext;
    }

    /// <inheritdoc />
    public async Task<string> DecryptAsync(
        string keyName,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        using System.Diagnostics.Activity? activity = VaultAwsActivitySource.Source.StartActivity(
            VaultAwsActivitySource.Operations.KmsDecrypt);
        activity?.SetTag(VaultAwsActivitySource.Tags.KeyName, keyName);

        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);

        DecryptRequest request = new()
        {
            CiphertextBlob = new MemoryStream(ciphertextBytes),
        };

        DecryptResponse response = await kmsClient.DecryptAsync(request, cancellationToken)
            .ConfigureAwait(false);

        string result = Encoding.UTF8.GetString(response.Plaintext.ToArray());

        LogDecryptSuccess(keyName);
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// AWS KMS does not expose a server-side rewrap endpoint.
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
    /// <remarks>AWS KMS ciphertext does not embed a key version — returns <c>null</c>.</remarks>
    public string? GetKeyVersion(string ciphertext) => null;

    [LoggerMessage(Level = LogLevel.Debug, Message = "KMS encrypt succeeded for key {KeyName}")]
    private partial void LogEncryptSuccess(string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "KMS decrypt succeeded for key {KeyName}")]
    private partial void LogDecryptSuccess(string keyName);
}
