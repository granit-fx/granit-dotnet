using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Webhooks.Notifications;

/// <summary>
/// Granit module for the webhooks notification bridge.
/// Routes webhook delivery-threshold integration events to administrators via
/// <c>Granit.Notifications</c>, with embedded HTML templates shipped for the
/// EN and FR baseline cultures.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for the webhooks notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitWebhooksNotificationsModule).Assembly);

        // Layout glob — covers all snake_case notification names. The host application
        // registers the actual `Layout.Email` template; if absent, templates render
        // without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("webhooks.*", "Layout.Email");
    }
}
