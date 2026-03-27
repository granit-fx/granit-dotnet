using Microsoft.Extensions.Diagnostics.HealthChecks;
using VaultSharp;
using VaultSystemHealth = VaultSharp.V1.SystemBackend.HealthStatus;

namespace Granit.Vault.HashiCorp.HealthChecks;

/// <summary>
/// Health check that verifies HashiCorp Vault connectivity and operational state via <c>sys/health</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Active → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Standby (passive replication) → <see cref="HealthCheckResult.Degraded"/>
///   (pod remains in load balancer; read operations still work)</item>
///   <item>Sealed or unreachable → <see cref="HealthCheckResult.Unhealthy"/>
///   (pod removed from load balancer)</item>
/// </list>
/// The response never exposes tokens, credentials, or secret values.
/// </remarks>
internal sealed class VaultHealthCheck(IVaultClient vaultClient) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // VaultSharp API does not expose cancellation — WaitAsync provides a defensive timeout.
            VaultSystemHealth status = await vaultClient.V1.System.GetHealthStatusAsync()
                .WaitAsync(s_healthCheckTimeout, cancellationToken).ConfigureAwait(false);

            if (status.Sealed)
            {
                return HealthCheckResult.Unhealthy("Vault sealed");
            }

            if (status.Standby)
            {
                return HealthCheckResult.Degraded("Vault standby");
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitize: never expose connection details or tokens in the message
            return HealthCheckResult.Unhealthy($"Vault unreachable: {ex.GetType().Name}");
        }
    }
}
