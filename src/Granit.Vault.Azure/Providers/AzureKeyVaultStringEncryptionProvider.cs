using Azure;
using Granit.Encryption;
using Granit.Encryption.Options;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.Providers;

/// <summary>
/// Bridges the async <see cref="ITransitEncryptionService"/> to the
/// synchronous <see cref="IStringEncryptionProvider"/> contract.
/// </summary>
internal sealed class AzureKeyVaultStringEncryptionProvider(
    ITransitEncryptionService transitEncryption,
    IOptions<StringEncryptionOptions> options) : IStringEncryptionProvider
{
    /// <summary>Provider name constant.</summary>
    public const string Name = "AzureKeyVault";

    private readonly string _keyName = options.Value.VaultKeyName;

    /// <inheritdoc />
    public string ProviderName => Name;

    /// <inheritdoc />
    public string Encrypt(string plainText) =>
        transitEncryption.EncryptAsync(_keyName, plainText).GetAwaiter().GetResult();

    /// <inheritdoc />
    public string? Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return null;
        }

        try
        {
            return transitEncryption.DecryptAsync(_keyName, cipherText).GetAwaiter().GetResult();
        }
        catch (RequestFailedException ex) when (ex.Status is 400 or 422)
        {
            return null;
        }
    }
}
