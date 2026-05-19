using Granit.Notifications.Abstractions;
using Granit.Notifications.Email.Internal;
using Granit.Notifications.Email.Options;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Notifications.Email.Extensions;

/// <summary>Extension methods for the email notification channel.</summary>
public static class EmailNotificationsServiceCollectionExtensions
{
    /// <summary>Adds the email notification channel with Keyed Services provider resolution.</summary>
    public static IServiceCollection AddGranitNotificationsEmail(
        this IServiceCollection services,
        Action<EmailChannelOptions>? configure = null)
    {
        services.AddOptions<EmailChannelOptions>()
            .BindConfiguration(EmailChannelOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.AddEmbeddedTemplates(typeof(EmailNotificationsServiceCollectionExtensions).Assembly);
        services.AddTemplateLayout("Notifications.*", "Layout.Email");
        return services;
    }
}
