using Granit.Notifications.Abstractions;
using Granit.Notifications.WhatsApp.Internal;
using Granit.Notifications.WhatsApp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.WhatsApp.Extensions;

/// <summary>Extension methods for the WhatsApp notification channel.</summary>
public static class WhatsAppNotificationsServiceCollectionExtensions
{
    /// <summary>Adds the WhatsApp notification channel with Keyed Services provider resolution.</summary>
    public static IServiceCollection AddGranitNotificationsWhatsApp(
        this IServiceCollection services,
        Action<WhatsAppChannelOptions>? configure = null)
    {
        services.AddOptions<WhatsAppChannelOptions>()
            .BindConfiguration(WhatsAppChannelOptions.SectionName)
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<WhatsAppChannelOptions>, WhatsAppChannelOptionsValidator>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped<INotificationChannel, WhatsAppNotificationChannel>();
        return services;
    }
}
