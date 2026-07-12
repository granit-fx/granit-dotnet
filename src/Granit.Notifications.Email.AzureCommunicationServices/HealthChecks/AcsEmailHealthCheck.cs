using Azure.Communication.Email;
using Azure.Identity;
using Granit.Notifications.Email.AzureCommunicationServices.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.AzureCommunicationServices.HealthChecks;

/// <summary>
/// Health check that verifies the Azure Communication Services email configuration.
/// </summary>
/// <remarks>
/// <para>
/// The ACS email API exposes no read-only operation, so this check validates configuration
/// and client construction only — it MUST NOT send a message: a real send on every readiness
/// probe generates bounces and cost, and mutates external state (mirrors the config-only
/// probes of the AwsSns and ACS SMS providers).
/// </para>
/// <list type="bullet">
///   <item>Sender + connection string / endpoint present and well-formed → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Missing or malformed configuration → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes connection strings or endpoint details.
/// </remarks>
internal sealed class AcsEmailHealthCheck(IOptions<AcsEmailOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        AcsEmailOptions opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.DefaultSenderEmail))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("ACS Email: DefaultSenderEmail is not configured"));
        }

        if (string.IsNullOrWhiteSpace(opts.ConnectionString) && string.IsNullOrWhiteSpace(opts.Endpoint))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("ACS Email: neither ConnectionString nor Endpoint is configured"));
        }

        try
        {
            // Construction parses the connection string / endpoint URI — catches malformed
            // configuration without any network call.
            _ = opts.ConnectionString is not null
                ? new EmailClient(opts.ConnectionString)
                : new EmailClient(new Uri(opts.Endpoint!), new DefaultAzureCredential());

            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or UriFormatException or InvalidOperationException)
        {
            // Sanitize: never expose connection strings, endpoints, or credentials
            return Task.FromResult(HealthCheckResult.Unhealthy($"ACS Email configuration invalid: {ex.GetType().Name}"));
        }
    }
}
