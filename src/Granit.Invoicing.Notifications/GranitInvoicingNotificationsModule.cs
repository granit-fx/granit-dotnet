using Granit.Invoicing.Notifications.Internal;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Invoicing.Notifications;

/// <summary>
/// Notification types and email templates for invoicing lifecycle events.
/// </summary>
[DependsOn(
    typeof(GranitInvoicingModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitInvoicingNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitInvoicingNotificationsModule).Assembly);
        context.Services.AddTemplateLayout("Invoicing.*", "Layout.Email");
        context.Services.AddSingleton<INotificationDefinitionProvider, InvoicingNotificationDefinitionProvider>();
    }
}
