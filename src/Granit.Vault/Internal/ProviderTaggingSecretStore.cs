using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;

namespace Granit.Vault.Internal;

/// <summary>
/// Minimal decorator that forwards calls to the concrete provider store and emits the
/// observable <c>granit.vault.secret.read</c> counter. Used when caching is disabled,
/// so the provider stays free of metrics-by-outcome concerns (the provider only emits
/// ActivitySource spans for the SDK call).
/// </summary>
internal sealed class ProviderTaggingSecretStore(
    ISecretStore inner,
    VaultMetrics metrics,
    IServiceProvider serviceProvider,
    string providerName) : ISecretStore
{
    /// <inheritdoc />
    public async Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? tenantId = SecretStoreDiagnostics.ResolveTenantId(serviceProvider);

        try
        {
            SecretDescriptor descriptor = await inner.GetSecretAsync(request, cancellationToken).ConfigureAwait(false);
            metrics.RecordSecretRead(tenantId, providerName, outcome: "ok", cached: false);
            return descriptor;
        }
        catch (SecretVaultException ex)
        {
            SecretStoreDiagnostics.RecordAndThrow(metrics, tenantId, providerName, cached: false, ex);
            throw;
        }
    }
}
