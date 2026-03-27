using Azure.Security.KeyVault.Keys;
using Granit.Vault.Azure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to Azure Key Vault by retrieving the configured key.
/// </summary>
internal sealed class AzureKeyVaultHealthCheck(
    KeyClient keyClient,
    IOptions<AzureKeyVaultOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            global::Azure.Response<KeyVaultKey> response = await keyClient
                .GetKeyAsync(options.Value.EncryptionKeyName, cancellationToken: cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return response.Value.Properties.Enabled == true
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Azure Key Vault key is disabled");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Azure Key Vault unreachable");
        }
    }
}
