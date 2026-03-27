using Granit.Encryption;
using Granit.Encryption.Options;
using Granit.Vault.Exceptions;
using Granit.Vault.Options;
using Microsoft.Extensions.Options;

namespace Granit.Vault.HashiCorp.Providers;

/// <summary>
/// String encryption provider backed by HashiCorp Vault Transit Engine.
/// Reserved for rare, high-security operations.
/// </summary>
internal sealed class HashiCorpVaultStringEncryptionProvider(
    ITransitEncryptionService transitEncryption,
    IOptions<StringEncryptionOptions> options,
    IOptions<ReEncryptionOptions>? reEncryptionOptions = null) : IStringEncryptionProvider
{
    private readonly string _keyName = options.Value.VaultKeyName;
    private readonly ISet<string> _retiredVersions = reEncryptionOptions?.Value.RetiredKeyVersions
        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string ProviderName => StringEncryptionOptions.VaultProviderName;

    /// <inheritdoc/>
    public string Encrypt(string plainText) =>
        transitEncryption.EncryptAsync(_keyName, plainText)
            .GetAwaiter()
            .GetResult();

    /// <inheritdoc/>
    public string? Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return null;
        }

        // Guard: throw loudly if ciphertext was encrypted with a retired key version.
        // This prevents silently returning garbage and ensures the re-encryption job
        // is run before any key version is marked as retired.
        string? keyVersion = transitEncryption.GetKeyVersion(cipherText);
        if (keyVersion is not null && _retiredVersions.Contains(keyVersion))
        {
            throw new RetiredKeyVersionException(keyVersion);
        }

        try
        {
            return transitEncryption.DecryptAsync(_keyName, cipherText)
                .GetAwaiter()
                .GetResult();
        }
        catch (VaultSharp.Core.VaultApiException ex)
            when (ex.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Invalid ciphertext or decryption error — Vault returned 400
            return null;
        }
    }
}
