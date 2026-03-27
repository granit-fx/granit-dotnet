using Granit.Encryption;
using Granit.Webhooks.Abstractions;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Production-grade <see cref="IWebhookSecretProtector"/> backed by <see cref="IStringEncryptionService"/>
/// from the Granit Encryption module. Encrypts signing secrets at rest using the application's
/// configured encryption provider (AES-256-CBC by default, Vault Transit for production).
/// </summary>
/// <remarks>
/// ISO 27001 A.8.24: secrets are encrypted at the application layer before persistence.
/// Key management is delegated to the encryption provider.
/// </remarks>
internal sealed class EncryptionWebhookSecretProtector(IStringEncryptionService encryptionService) : IWebhookSecretProtector
{
    public ValueTask<string> ProtectAsync(string plainSecret, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(encryptionService.Encrypt(plainSecret));

    public ValueTask<string> UnprotectAsync(string protectedSecret, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(encryptionService.Decrypt(protectedSecret)
            ?? throw new InvalidOperationException("Failed to decrypt webhook signing secret — key may have been rotated or corrupted."));
}
