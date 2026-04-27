using Granit.Modularity;
using Granit.Notifications;
using Granit.Privacy.Notifications.GlobalContexts;
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
    typeof(GranitTemplatingModule))]
public sealed class GranitPrivacyNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddTemplateGlobalContext<PrivacyContactGlobalContext>();
}
