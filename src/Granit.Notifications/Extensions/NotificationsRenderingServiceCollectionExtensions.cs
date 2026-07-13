using Granit.Notifications.Internal;
using Granit.Notifications.Rendering;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Notifications.Extensions;

/// <summary>Registers the template-backed notification content renderer.</summary>
public static class NotificationsRenderingServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="INotificationContentRenderer"/> (template-backed, soft dependency on
    /// <c>Granit.Templating</c>: hosts without a template engine get <see langword="null"/>
    /// renders and channels keep their minimal fallback). Called by
    /// <c>AddGranitNotifications</c>; standalone use is only needed in custom harnesses.
    /// </summary>
    public static IServiceCollection AddGranitNotificationContentRenderer(this IServiceCollection services)
    {
        services.TryAddSingleton<INotificationContentRenderer, TemplateNotificationContentRenderer>();

        // Channel-agnostic text/markdown fallback templates (Notifications.Default.{txt,md},
        // EN + FR baseline — other cultures via scripts/translate-templates.py).
        services.AddEmbeddedTemplates(typeof(TemplateNotificationContentRenderer).Assembly);

        return services;
    }
}
