using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Granit.UserSessions.Notifications.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.UserSessions.Notifications;

/// <summary>
/// Granit module for the user-session notification bridge.
/// Routes <see cref="SuspiciousUserSessionDetectedEto"/> to users via <c>Granit.Notifications</c>,
/// sending a brand-neutral "suspicious sign-in" security alert email (location and plain-language
/// reason, never the raw IP). Recipient resolution is host-wired via <c>IRecipientResolver</c> —
/// this module does not depend on <c>Granit.Identity</c>.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule),
    typeof(GranitIdentityAbstractionsModule))]
public sealed class GranitUserSessionsNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates (2 notifications × EN/FR baseline). Apps can override
        // any of them at runtime through the Granit.Templating admin API.
        context.Services.AddEmbeddedTemplates(typeof(GranitUserSessionsNotificationsModule).Assembly);

        // Layout glob — the host application registers the actual `Layout.Email` template; if
        // absent, templates render without a layout (warning logged, no crash).
        context.Services.AddTemplateLayout("user_sessions.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, UserSessionsNotificationDefinitionProvider>();

        context.Services.AddOptions<Options.UserSessionsNotificationOptions>()
            .BindConfiguration(Options.UserSessionsNotificationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
