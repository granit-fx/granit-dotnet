using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Subscriptions.Notifications.Internal;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Subscriptions.Notifications;

/// <summary>
/// Notification types and email templates for subscription lifecycle events.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitSubscriptionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitSubscriptionsNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitSubscriptionsNotificationsModule).Assembly);
        context.Services.AddTemplateLayout("Subscriptions.*", "Layout.Email");
        context.Services.AddSingleton<INotificationDefinitionProvider, SubscriptionsNotificationDefinitionProvider>();
    }
}
