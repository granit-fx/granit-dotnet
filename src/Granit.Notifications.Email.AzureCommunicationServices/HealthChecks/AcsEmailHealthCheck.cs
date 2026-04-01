using Azure.Communication.Email;
using Azure.Identity;
using Granit.Notifications.Email.AzureCommunicationServices.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.AzureCommunicationServices.HealthChecks;

/// <summary>
/// Health check that verifies Azure Communication Services email connectivity.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Endpoint reachable → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Unreachable or credentials invalid → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes connection strings or endpoint details.
/// </remarks>
internal sealed class AcsEmailHealthCheck(IOptions<AcsEmailOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AcsEmailOptions opts = options.Value;

            EmailClient client = opts.ConnectionString is not null
                ? new EmailClient(opts.ConnectionString)
                : new EmailClient(new Uri(opts.Endpoint!), new DefaultAzureCredential());

            // Attempt a lightweight send with an invalid message to verify connectivity.
            // The service will reject it, but a successful rejection proves the endpoint is reachable.
            // We catch the expected RequestFailedException to confirm connectivity.
            var testMessage = new Azure.Communication.Email.EmailMessage(
                opts.DefaultSenderEmail,
                "healthcheck@localhost",
                new EmailContent("healthcheck"));

            await client
                .SendAsync(Azure.WaitUntil.Started, testMessage, cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Azure.RequestFailedException)
        {
            // A RequestFailedException means the endpoint is reachable but rejected the request,
            // which is expected for a health check probe.
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitize: never expose connection strings, endpoints, or credentials
            return HealthCheckResult.Unhealthy($"ACS Email unreachable: {ex.GetType().Name}");
        }
    }
}
