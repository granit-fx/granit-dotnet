using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Granit.Diagnostics;
using Granit.Notifications.AwsSns.Sms.Diagnostics;
using Granit.Notifications.AwsSns.Sms.HealthChecks;
using Granit.Notifications.AwsSns.Sms.Internal;
using Granit.Notifications.AwsSns.Sms.Options;
using Granit.Notifications.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSns.Sms.Extensions;

/// <summary>Extension methods for the AWS SNS SMS provider.</summary>
public static class SnsSmsServiceCollectionExtensions
{
    /// <summary>Registers the SNS SMS sender as Keyed Service with key "AwsSns".</summary>
    public static IServiceCollection AddGranitNotificationsAwsSnsSms(
        this IServiceCollection services,
        Action<AwsSnsSmsOptions>? configure = null)
    {
        services.AddOptions<AwsSnsSmsOptions>()
            .BindConfiguration(AwsSnsSmsOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AwsSnsSmsOptions>, AwsSnsSmsOptionsValidator>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IAwsSnsSmsTransport>(sp =>
        {
            AwsSnsSmsOptions opts = sp.GetRequiredService<IOptions<AwsSnsSmsOptions>>().Value;
            var region = RegionEndpoint.GetBySystemName(opts.Region);

            IAmazonSimpleNotificationService client = !string.IsNullOrEmpty(opts.AccessKeyId)
                ? new AmazonSimpleNotificationServiceClient(
                    new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey), region)
                : new AmazonSimpleNotificationServiceClient(region);

            return new AwsSnsSmsTransport(client);
        });

        services.AddKeyedSingleton<ISmsSender, AwsSnsSmsSender>("AwsSns");

        GranitActivitySourceRegistry.Register(NotificationsAwsSnsSmsActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the SNS SMS health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    public static IHealthChecksBuilder AddGranitAwsSnsSmsHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "sns-sms",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<AwsSnsSmsHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<AwsSnsSmsHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout));
    }
}
