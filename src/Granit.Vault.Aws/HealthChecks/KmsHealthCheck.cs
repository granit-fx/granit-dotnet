using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Granit.Vault.Aws.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Aws.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to AWS KMS by describing the configured key.
/// </summary>
internal sealed class KmsHealthCheck(
    IAmazonKeyManagementService kmsClient,
    IOptions<AwsVaultOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DescribeKeyRequest request = new() { KeyId = options.Value.KmsKeyId };

            DescribeKeyResponse response = await kmsClient
                .DescribeKeyAsync(request, cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return response.KeyMetadata.Enabled == true
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("KMS key is disabled");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("KMS unreachable");
        }
    }
}
