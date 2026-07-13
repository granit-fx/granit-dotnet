using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Granit.Diagnostics;
using Granit.Extensions;
using Granit.Notifications.AwsSes.Diagnostics;
using Granit.Notifications.AwsSes.HealthChecks;
using Granit.Notifications.AwsSes.Internal;
using Granit.Notifications.AwsSes.Options;
using Granit.Notifications.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSes.Extensions;

/// <summary>Extension methods for the Amazon SES email provider.</summary>
public static class SesEmailServiceCollectionExtensions
{
    /// <summary>Registers the Amazon SES email sender as Keyed Service with key "AwsSes".</summary>
    public static IServiceCollection AddGranitNotificationsAwsSes(
        this IServiceCollection services,
        Action<AwsSesOptions>? configure = null)
    {
        services.AddGranitProviderOptions<AwsSesOptions, AwsSesOptionsValidator>(AwsSesOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<Func<IAwsSesTransport>>(sp =>
        {
            AwsSesOptions opts = sp.GetRequiredService<IOptions<AwsSesOptions>>().Value;
            return () =>
            {
                var config = new AmazonSimpleEmailServiceV2Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region),
                    Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds),
                };

                IAmazonSimpleEmailServiceV2 client = opts.AccessKeyId is not null
                    ? new AmazonSimpleEmailServiceV2Client(
                        new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey), config)
                    : new AmazonSimpleEmailServiceV2Client(config);

                return new AwsSesTransport(client);
            };
        });

        services.AddKeyedSingleton<IEmailSender, AwsSesEmailSender>("AwsSes");

        GranitActivitySourceRegistry.Register(NotificationsAwsSesActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the Amazon SES health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"aws-ses"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitAwsSesHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "aws-ses",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new AwsSesHealthCheck(sp.GetRequiredService<IOptions<AwsSesOptions>>()),
            failureStatus,
            ["readiness", "startup"],
            timeout));
}
