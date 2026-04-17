using Granit.MultiTenancy;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;

namespace Granit.Vault.Internal;

/// <summary>
/// Shared diagnostics helpers used by <see cref="CachedSecretStore"/> and
/// <see cref="ProviderTaggingSecretStore"/>. Centralises tenant resolution and
/// exception-to-outcome mapping so the two decorators stay in lockstep.
/// </summary>
internal static class SecretStoreDiagnostics
{
    /// <summary>
    /// Resolves the current tenant id, or <c>null</c> when no tenant is active.
    /// Safe to call when <see cref="ICurrentTenant"/> is not registered — returns <c>null</c>.
    /// </summary>
    internal static string? ResolveTenantId(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var currentTenant = serviceProvider.GetService(typeof(ICurrentTenant)) as ICurrentTenant;
        return currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;
    }

    /// <summary>
    /// Maps a <see cref="SecretVaultException"/> (or subtype) to its canonical outcome tag
    /// used by the <c>granit.vault.secret.read</c> counter.
    /// Returns <c>null</c> if the exception is not part of the vault taxonomy — the
    /// counter must not be emitted in that case (unknown failure, not vault-attributable).
    /// </summary>
    internal static string? TryMapOutcome(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            SecretNotFoundException => "not_found",
            SecretAccessDeniedException => "denied",
            SecretVaultTransientException => "transient",
            SecretVaultException => "error",
            _ => null,
        };
    }

    /// <summary>
    /// Records an outcome if the exception belongs to the vault taxonomy, then rethrows.
    /// Factored out so both decorators share an identical observe-then-throw path.
    /// </summary>
    internal static void RecordAndThrow(
        VaultMetrics metrics,
        string? tenantId,
        string providerName,
        bool cached,
        Exception exception)
    {
        string? outcome = TryMapOutcome(exception);
        if (outcome is not null)
        {
            metrics.RecordSecretRead(tenantId, providerName, outcome, cached);
        }
    }
}
