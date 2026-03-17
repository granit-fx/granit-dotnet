using System.Text;
using System.Text.RegularExpressions;
using Granit.Vault.HashiCorp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Granit.Vault.HashiCorp.Services;

/// <summary>
/// Implementation of <see cref="ITransitEncryptionService"/> via HashiCorp Vault Transit Engine.
/// </summary>
public sealed partial class HashiCorpTransitEncryptionService(
    IVaultClient vaultClient,
    IOptions<HashiCorpVaultOptions> options,
    ILogger<HashiCorpTransitEncryptionService> logger) : ITransitEncryptionService
{
    private readonly HashiCorpVaultOptions _options = options.Value;

    // Vault Transit ciphertext format: vault:v{N}:...
    [GeneratedRegex(@"^vault:(?<version>v\d+):", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VaultVersionRegex();

    public async Task<string> EncryptAsync(
        string keyName,
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        string base64Plaintext = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

        // VaultSharp API does not expose cancellation — WaitAsync provides a defensive timeout.
        Secret<EncryptionResponse> result = await vaultClient.V1.Secrets.Transit.EncryptAsync(
            keyName,
            new EncryptRequestOptions
            {
                Base64EncodedPlainText = base64Plaintext
            },
            mountPoint: _options.TransitMountPoint).WaitAsync(cancellationToken).ConfigureAwait(false);

        LogEncrypted(logger, keyName);
        return result.Data.CipherText;
    }

    public async Task<string> DecryptAsync(
        string keyName,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        // VaultSharp API does not expose cancellation — WaitAsync provides a defensive timeout.
        Secret<DecryptionResponse> result = await vaultClient.V1.Secrets.Transit.DecryptAsync(
            keyName,
            new DecryptRequestOptions
            {
                CipherText = ciphertext
            },
            mountPoint: _options.TransitMountPoint).WaitAsync(cancellationToken).ConfigureAwait(false);

        byte[] bytes = Convert.FromBase64String(result.Data.Base64EncodedPlainText);
        LogDecrypted(logger, keyName);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <inheritdoc />
    public async Task<string> RewrapAsync(
        string keyName,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        // VaultSharp API does not expose cancellation — WaitAsync provides a defensive timeout.
        Secret<EncryptionResponse> result = await vaultClient.V1.Secrets.Transit.RewrapAsync(
            keyName,
            new RewrapRequestOptions
            {
                CipherText = ciphertext
            },
            mountPoint: _options.TransitMountPoint).WaitAsync(cancellationToken).ConfigureAwait(false);

        LogRewrapped(logger, keyName);
        return result.Data.CipherText;
    }

    /// <inheritdoc />
    public string? GetKeyVersion(string ciphertext)
    {
        Match match = VaultVersionRegex().Match(ciphertext);
        return match.Success ? match.Groups["version"].Value : null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Data encrypted with Transit key {KeyName}")]
    private static partial void LogEncrypted(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Data decrypted with Transit key {KeyName}")]
    private static partial void LogDecrypted(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Data rewrapped with Transit key {KeyName}")]
    private static partial void LogRewrapped(ILogger logger, string keyName);
}
