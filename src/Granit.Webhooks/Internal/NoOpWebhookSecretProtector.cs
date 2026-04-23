using Granit.Webhooks.Abstractions;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Pass-through implementation of <see cref="IWebhookSecretProtector"/> that stores secrets
/// in plain text. Suitable for development and tests only.
/// </summary>
/// <remarks>
/// For production (GDPR/ISO 27001), register a Vault-backed protector via
/// <c>AddGranitWebhooksWithVaultSecrets(keyName)</c>.
/// </remarks>
internal sealed class NoOpWebhookSecretProtector : IWebhookSecretProtector
{
    public ValueTask<string> ProtectAsync(string plainSecret, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(plainSecret);

    public ValueTask<string> UnprotectAsync(string protectedSecret, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(protectedSecret);
}
