namespace Granit.Vault;

/// <summary>
/// Reads arbitrary secrets from the configured vault provider.
/// One implementation per provider (HashiCorp KV v2, Azure Key Vault,
/// AWS Secrets Manager, Google Cloud Secret Manager).
/// </summary>
/// <remarks>
/// <para>
/// Use this interface for application-level secrets that are NOT database credentials
/// (use <see cref="IDatabaseCredentialProvider"/> for those) and NOT transit ciphertext
/// (use <see cref="ITransitEncryptionService"/> for that): mTLS certificates, signing
/// keys, SMTP credentials, third-party API keys, etc.
/// </para>
/// <para>
/// <b>Retry policy:</b> this interface does not perform any automatic retry.
/// Transient failures (throttling, 503, network timeouts) surface as
/// <see cref="Exceptions.SecretVaultTransientException"/>. Consumers that need retry
/// semantics wrap calls in their own Polly pipeline — the retry policy belongs to the
/// caller because it depends on SLOs that vary per context (startup, hot path, batch).
/// </para>
/// <para>
/// <b>Caching:</b> a caching decorator is applied transparently when
/// <c>Vault:SecretCacheSeconds &gt; 0</c> in configuration. Disabled by default for
/// security-by-default.
/// </para>
/// </remarks>
public interface ISecretStore
{
    /// <summary>
    /// Retrieves a secret. Throws <see cref="Exceptions.SecretNotFoundException"/> if absent.
    /// </summary>
    /// <param name="request">The secret to read (name and optional version).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SecretDescriptor"/> with the payload and metadata.</returns>
    /// <exception cref="Exceptions.SecretNotFoundException">The secret does not exist.</exception>
    /// <exception cref="Exceptions.SecretAccessDeniedException">The vault denied access (403).</exception>
    /// <exception cref="Exceptions.SecretVaultTransientException">Transient failure (429/503/timeout) — safe to retry.</exception>
    /// <exception cref="Exceptions.SecretVaultConfigurationException">The vault is misconfigured.</exception>
    Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a secret, returning <c>null</c> if it does not exist.
    /// All other failure modes (access denied, transient, configuration) propagate
    /// as exceptions — callers must not confuse a missing secret with a network/permissions issue.
    /// </summary>
    /// <remarks>
    /// Default interface implementation — providers MUST NOT override this method.
    /// The architecture test <c>VaultConventionTests</c> enforces that no concrete
    /// implementation shadows <c>TryGetSecretAsync</c>; doing so would risk swallowing
    /// infra failures under the guise of a "missing" secret.
    /// </remarks>
    async Task<SecretDescriptor?> TryGetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetSecretAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exceptions.SecretNotFoundException)
        {
            return null;
        }
    }
}
