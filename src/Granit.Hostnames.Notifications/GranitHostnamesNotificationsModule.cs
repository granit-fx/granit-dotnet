using Granit.Hostnames.Notifications.Internal;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Hostnames.Notifications;

/// <summary>
/// Granit module for the hostname notification bridge.
/// Routes <see cref="GranitHostnamesModule"/> DNS verification events to the
/// platform operator via <c>Granit.Notifications</c>.
/// </summary>
[DependsOn(
    typeof(GranitHostnamesModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitHostnamesNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitHostnamesNotificationsModule).Assembly);

        context.Services.AddTemplateLayout("hostnames.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, HostnamesNotificationDefinitionProvider>();
    }
}
