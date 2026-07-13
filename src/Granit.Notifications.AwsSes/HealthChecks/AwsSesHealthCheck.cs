using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Granit.Notifications.AwsSes.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSes.HealthChecks;

/// <summary>
/// Health check that verifies Amazon SES connectivity by calling
/// <see cref="IAmazonSimpleEmailServiceV2.GetAccountAsync"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Account reachable and sending enabled → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Account reachable but sending paused → <see cref="HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or credentials invalid → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes credentials or region details.
/// </remarks>
internal sealed class AwsSesHealthCheck(IOptions<AwsSesOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AwsSesOptions opts = options.Value;

            var config = new AmazonSimpleEmailServiceV2Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region),
                Timeout = TimeSpan.FromSeconds(Math.Min(opts.TimeoutSeconds, 10)),
            };

            using AmazonSimpleEmailServiceV2Client client = opts.AccessKeyId is not null
                ? new AmazonSimpleEmailServiceV2Client(
                    new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey), config)
                : new AmazonSimpleEmailServiceV2Client(config);

            GetAccountResponse account = await client
                .GetAccountAsync(new GetAccountRequest(), cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (account.SendingEnabled is true)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Degraded("SES sending is paused on this account.");
        }
        catch (Exception ex)
        {
            // Sanitize: never expose region, credentials, or account details
            return HealthCheckResult.Unhealthy($"SES unreachable: {ex.GetType().Name}");
        }
    }
}
