namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Protects and unprotects webhook signing secrets before persistence and before use.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is <c>NoOpWebhookSecretProtector</c> which stores secrets in plain text.
/// For production (GDPR/ISO 27001), replace with a Vault-backed implementation via
/// <c>AddGranitWebhooksWithVaultSecrets(keyName)</c> or register a custom implementation.
/// </para>
/// <para>
/// Never log the values returned by either method.
/// </para>
/// </remarks>
public interface IWebhookSecretProtector
{
    /// <summary>
    /// Protects a plain-text signing secret before it is persisted in the subscription store.
    /// </summary>
    ValueTask<string> ProtectAsync(string plainSecret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers the plain-text signing secret from its protected representation,
    /// immediately before HMAC computation.
    /// </summary>
    ValueTask<string> UnprotectAsync(string protectedSecret, CancellationToken cancellationToken = default);
}
