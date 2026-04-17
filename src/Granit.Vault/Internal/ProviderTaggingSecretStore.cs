using Granit.MultiTenancy;
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

        string? tenantId = ResolveTenantId();

        try
        {
            SecretDescriptor descriptor = await inner.GetSecretAsync(request, cancellationToken).ConfigureAwait(false);
            metrics.RecordSecretRead(tenantId, providerName, outcome: "ok", cached: false);
            return descriptor;
        }
        catch (SecretNotFoundException)
        {
            metrics.RecordSecretRead(tenantId, providerName, outcome: "not_found", cached: false);
            throw;
        }
        catch (SecretAccessDeniedException)
        {
            metrics.RecordSecretRead(tenantId, providerName, outcome: "denied", cached: false);
            throw;
        }
        catch (SecretVaultTransientException)
        {
            metrics.RecordSecretRead(tenantId, providerName, outcome: "transient", cached: false);
            throw;
        }
        catch (SecretVaultException)
        {
            metrics.RecordSecretRead(tenantId, providerName, outcome: "error", cached: false);
            throw;
        }
    }

    private string? ResolveTenantId()
    {
        var currentTenant = serviceProvider.GetService(typeof(ICurrentTenant)) as ICurrentTenant;
        return currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;
    }
}
