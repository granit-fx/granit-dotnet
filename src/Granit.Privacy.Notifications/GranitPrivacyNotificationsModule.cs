using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Privacy.Notifications.GlobalContexts;
using Granit.Privacy.Notifications.Internal;
using Granit.Privacy.Regulations;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Granit module for the privacy notification bridge.
/// Routes deletion reminder and confirmation events to users via <c>Granit.Notifications</c>,
/// and exposes the data controller and DPO contact (GDPR Art. 13) as the
/// <c>{{ privacy }}</c> template global context.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitPrivacyModule),
    typeof(GranitPrivacyRegulationsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitPrivacyNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTemplateGlobalContext<PrivacyContactGlobalContext>();

        // Ship the embedded HTML templates for all 8 privacy notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitPrivacyNotificationsModule).Assembly);

        // Layout glob — covers all snake_case notification names. The host application
        // registers the actual `Layout.Email` template; if absent, templates render
        // without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("privacy.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, PrivacyNotificationDefinitionProvider>();
    }
}
