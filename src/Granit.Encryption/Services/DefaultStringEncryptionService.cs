using Granit.Encryption.Options;
using Microsoft.Extensions.Options;

namespace Granit.Encryption.Services;

/// <summary>
/// Implementation of <see cref="IStringEncryptionService"/> that delegates
/// to the <see cref="IStringEncryptionProvider"/> selected by configuration.
/// </summary>
public sealed class DefaultStringEncryptionService(
    IEnumerable<IStringEncryptionProvider> providers,
    IOptions<StringEncryptionOptions> options) : IStringEncryptionService
{
    private readonly IStringEncryptionProvider _provider =
        providers.FirstOrDefault(p => p.ProviderName == options.Value.ProviderName)
        ?? throw new InvalidOperationException(
            $"No encryption provider named '{options.Value.ProviderName}' is registered. " +
            $"Available providers: {string.Join(", ", providers.Select(p => p.ProviderName))}");

    /// <inheritdoc/>
    public string Encrypt(string plainText) => _provider.Encrypt(plainText);

    /// <inheritdoc/>
    public string? Decrypt(string cipherText) => _provider.Decrypt(cipherText);
}
