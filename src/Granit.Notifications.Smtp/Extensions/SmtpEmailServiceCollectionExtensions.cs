using Granit.Notifications.Email;
using Granit.Notifications.Smtp.HealthChecks;
using Granit.Notifications.Smtp.Internal;
using Granit.Notifications.Smtp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Notifications.Smtp.Extensions;

/// <summary>Extension methods for the MailKit SMTP email provider.</summary>
public static class SmtpEmailServiceCollectionExtensions
{
    /// <summary>Registers the MailKit SMTP email sender as Keyed Service with key "Smtp".</summary>
    public static IServiceCollection AddGranitNotificationsSmtp(
        this IServiceCollection services,
        Action<SmtpOptions>? configure = null)
    {
        services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddKeyedSingleton<IEmailSender, MailKitEmailSender>("Smtp");
        return services;
    }

    /// <summary>
    /// Adds an SMTP connectivity health check tagged <c>"readiness"</c>.
    /// Performs an EHLO handshake with optional authentication.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"smtp"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitSmtpHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "smtp",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<SmtpHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<SmtpHealthCheck>(),
            failureStatus,
            ["readiness"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
