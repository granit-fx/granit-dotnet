using Google.Cloud.Kms.V1;
using Granit.Vault.GoogleCloud.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.GoogleCloud.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to Google Cloud KMS by getting the configured crypto key.
/// </summary>
internal sealed class CloudKmsHealthCheck(
    KeyManagementServiceClient kmsClient,
    IOptions<GoogleCloudVaultOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GoogleCloudVaultOptions opts = options.Value;
            CryptoKeyName keyName = new(opts.ProjectId, opts.Location, opts.KeyRing, opts.CryptoKey);

            CryptoKey response = await kmsClient
                .GetCryptoKeyAsync(keyName, cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return response.Primary.State == CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cloud KMS key is disabled");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Cloud KMS unreachable: {ex.GetType().Name}");
        }
    }
}
