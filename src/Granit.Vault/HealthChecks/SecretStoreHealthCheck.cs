using Granit.Vault.Exceptions;
using Granit.Vault.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.HealthChecks;

/// <summary>
/// Provider-agnostic health check that reads a canary secret through
/// <see cref="ISecretStore"/>. Detects permission regressions, provider outages and
/// misconfiguration that the existing connectivity health checks (which call
/// provider-specific endpoints like <c>sys/health</c> or <c>DescribeKey</c>) may miss.
/// </summary>
/// <remarks>
/// Enabled only when <see cref="SecretStoreOptions.HealthCheckSecretName"/> is set —
/// otherwise the check returns <see cref="HealthStatus.Healthy"/> with a "not configured"
/// description so adopters who do not use the feature aren't forced to wire a canary.
/// </remarks>
internal sealed class SecretStoreHealthCheck(
    ISecretStore secretStore,
    IOptions<SecretStoreOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string? canary = options.Value.HealthCheckSecretName;
        if (string.IsNullOrWhiteSpace(canary))
        {
            return HealthCheckResult.Healthy("SecretStore canary not configured (Vault:SecretStore:HealthCheckSecretName).");
        }

        try
        {
            _ = await secretStore.GetSecretAsync(SecretRequest.Latest(canary), cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("SecretStore canary retrieved successfully.");
        }
        catch (SecretNotFoundException)
        {
            return HealthCheckResult.Unhealthy(
                "SecretStore canary not found. Verify Vault:SecretStore:HealthCheckSecretName is provisioned in the vault.");
        }
        catch (SecretAccessDeniedException)
        {
            return HealthCheckResult.Unhealthy(
                "SecretStore canary access denied. Verify the application principal has read permission.");
        }
        catch (SecretVaultTransientException)
        {
            return HealthCheckResult.Degraded("SecretStore is reporting a transient failure.");
        }
        catch (SecretVaultException ex)
        {
            return HealthCheckResult.Unhealthy("SecretStore returned an error.", ex);
        }
    }
}
