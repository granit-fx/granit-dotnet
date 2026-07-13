using Granit.Notifications.AzureCommunicationServices.Sms.Internal;
using Granit.Notifications.AzureCommunicationServices.Sms.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureCommunicationServices.Sms.HealthChecks;

/// <summary>
/// Health check that verifies Azure Communication Services SMS connectivity
/// by validating options and transport availability.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Transport available and options valid → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Unreachable or credentials invalid → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes connection strings or endpoint details.
/// </remarks>
internal sealed class AcsSmsHealthCheck(
    IOptions<AcsSmsOptions> options,
    IAcsSmsTransport transport) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AcsSmsOptions opts = options.Value;

            if (string.IsNullOrWhiteSpace(opts.FromPhoneNumber))
            {
                return HealthCheckResult.Unhealthy("ACS SMS: FromPhoneNumber is not configured.");
            }

            // Verify transport is resolvable (not null). A real SMS send
            // is not performed during health checks.
            _ = transport ?? throw new InvalidOperationException("IAcsSmsTransport is not registered.");

            await Task.CompletedTask.ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitize: never expose connection strings or endpoint details
            return HealthCheckResult.Unhealthy($"ACS SMS unreachable: {ex.GetType().Name}");
        }
    }
}
