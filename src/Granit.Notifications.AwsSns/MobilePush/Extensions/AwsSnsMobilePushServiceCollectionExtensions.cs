using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Granit.Diagnostics;
using Granit.Notifications.AwsSns.MobilePush.Diagnostics;
using Granit.Notifications.AwsSns.MobilePush.HealthChecks;
using Granit.Notifications.AwsSns.MobilePush.Internal;
using Granit.Notifications.AwsSns.MobilePush.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSns.MobilePush.Extensions;

/// <summary>Extension methods for the AWS SNS mobile push provider.</summary>
public static class AwsSnsMobilePushServiceCollectionExtensions
{
    /// <summary>Registers the SNS mobile push sender as Keyed Service with key "AwsSns".</summary>
    public static IServiceCollection AddGranitNotificationsAwsSnsMobilePush(
        this IServiceCollection services,
        Action<AwsSnsMobilePushOptions>? configure = null)
    {
        services.AddOptions<AwsSnsMobilePushOptions>()
            .BindConfiguration(AwsSnsMobilePushOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AwsSnsMobilePushOptions>, AwsSnsMobilePushOptionsValidator>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IAwsSnsMobilePushTransport>(sp =>
        {
            AwsSnsMobilePushOptions opts = sp.GetRequiredService<IOptions<AwsSnsMobilePushOptions>>().Value;
            var region = RegionEndpoint.GetBySystemName(opts.Region);

            IAmazonSimpleNotificationService client = !string.IsNullOrEmpty(opts.AccessKeyId)
                ? new AmazonSimpleNotificationServiceClient(
                    new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey), region)
                : new AmazonSimpleNotificationServiceClient(region);

            return new AwsSnsMobilePushTransport(client);
        });

        services.AddKeyedSingleton<IMobilePushSender, AwsSnsMobilePushSender>("AwsSns");

        GranitActivitySourceRegistry.Register(NotificationsAwsSnsMobilePushActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the SNS mobile push health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    public static IHealthChecksBuilder AddGranitAwsSnsMobilePushHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "aws-sns-mobile-push",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<AwsSnsMobilePushHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<AwsSnsMobilePushHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout));
    }
}
