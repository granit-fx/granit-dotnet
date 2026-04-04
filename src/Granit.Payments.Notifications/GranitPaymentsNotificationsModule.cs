using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Payments.Notifications.Internal;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Payments.Notifications;

/// <summary>
/// Notification types and email templates for payment events.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitPaymentsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitPaymentsNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitPaymentsNotificationsModule).Assembly);
        context.Services.AddTemplateLayout("Payments.*", "Layout.Email");
        context.Services.AddSingleton<INotificationDefinitionProvider, PaymentsNotificationDefinitionProvider>();
    }
}
