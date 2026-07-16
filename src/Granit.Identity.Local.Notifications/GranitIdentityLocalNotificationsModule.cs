using Granit.Auditing;
using Granit.Identity.Local.Notifications.Internal;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Notifications;

/// <summary>
/// Granit module for identity notification bridge.
/// Routes local identity events (registration, password reset, email confirmation,
/// account lockout, 2FA changes, impersonation) to users via <c>Granit.Notifications</c>.
/// </summary>
[DependsOn(
    typeof(GranitAuditingAbstractionsModule),
    typeof(GranitIdentityLocalModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitIdentityLocalNotificationsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitIdentityLocalNotificationsModule).Assembly);
        context.Services.AddTemplateLayout("identity.*", "Layout.Email");
        context.Services.AddSingleton<INotificationDefinitionProvider, IdentityNotificationDefinitionProvider>();

        context.Services.AddOptions<Options.IdentityNotificationOptions>()
            .BindConfiguration(Options.IdentityNotificationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
