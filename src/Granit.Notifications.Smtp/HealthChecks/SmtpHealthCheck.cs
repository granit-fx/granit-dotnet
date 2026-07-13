using Granit.Notifications.Smtp.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Smtp.HealthChecks;

/// <summary>
/// Health check that verifies SMTP server connectivity by performing an EHLO handshake
/// via MailKit.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Connect + optional auth succeeds → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Auth failure → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>Unreachable or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes credentials, hostnames, or ports.
/// </remarks>
internal sealed class SmtpHealthCheck(IOptions<SmtpOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Wrap the entire SMTP handshake (connect + auth + disconnect) with a defensive timeout.
            await VerifySmtpAsync(cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitize: never expose host, port, or credentials in the message
            return HealthCheckResult.Unhealthy($"SMTP unreachable: {ex.GetType().Name}");
        }
    }

    private async Task VerifySmtpAsync(CancellationToken cancellationToken)
    {
        SmtpOptions smtp = options.Value;
        SecureSocketOptions socketOptions = smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        using SmtpClient client = new();
        client.Timeout = Math.Min(smtp.TimeoutSeconds, 10) * 1000;

        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }

        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }
}
