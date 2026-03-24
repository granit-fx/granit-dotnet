using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Email.Scaleway.Diagnostics;
using Granit.Notifications.Email.Scaleway.HealthChecks;
using Granit.Notifications.Email.Scaleway.Internal;
using Granit.Notifications.Email.Scaleway.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Notifications.Email.Scaleway.Extensions;

/// <summary>Extension methods for the Scaleway Transactional Email provider.</summary>
public static class ScalewayEmailServiceCollectionExtensions
{
    private const string ProviderKey = "Scaleway";

    /// <summary>Registers the Scaleway email sender as Keyed Service with key "Scaleway".</summary>
    public static IServiceCollection AddGranitNotificationsEmailScaleway(
        this IServiceCollection services,
        Action<ScalewayEmailOptions>? configure = null)
    {
        services.AddOptions<ScalewayEmailOptions>()
            .BindConfiguration(ScalewayEmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddGranitHttpClient(ProviderKey, (sp, client) =>
        {
            ScalewayEmailOptions opts = sp
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ScalewayEmailOptions>>().Value;
            client.BaseAddress = new Uri(string.Concat(
                opts.BaseUrl.TrimEnd('/'), "/regions/", opts.Region.TrimEnd('/'), "/"));
            client.DefaultRequestHeaders.Add("X-Auth-Token", opts.SecretKey);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddSingleton<ScalewayEmailSender>();
        services.AddKeyedSingleton<IEmailSender>(
            ProviderKey, (sp, _) => sp.GetRequiredService<ScalewayEmailSender>());

        GranitActivitySourceRegistry.Register(NotificationsEmailScalewayActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the Scaleway email health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"scaleway-email"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitScalewayEmailHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "scaleway-email",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new ScalewayEmailHealthCheck(sp.GetRequiredService<IHttpClientFactory>()),
            failureStatus,
            ["readiness", "startup"],
            timeout));
}
