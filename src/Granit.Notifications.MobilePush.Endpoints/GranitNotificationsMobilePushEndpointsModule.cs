using Granit.Modularity;
using Granit.Notifications.Endpoints;
using Granit.Notifications.MobilePush.Endpoints.Options;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Notifications.MobilePush.Endpoints;

/// <summary>
/// Granit module for the mobile push device token HTTP endpoints.
/// </summary>
/// <remarks>
/// Opt-in companion to <see cref="GranitNotificationsEndpointsModule"/>: reference this package and
/// call <c>MapGranitMobilePushTokens()</c> to expose the token-management routes. Keeps the mobile
/// push channel out of hosts that do not use it.
/// </remarks>
[DependsOn(
    typeof(GranitNotificationsEndpointsModule),
    typeof(GranitNotificationsMobilePushModule),
    typeof(GranitValidationModule))]
public sealed class GranitNotificationsMobilePushEndpointsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddOptions<MobilePushEndpointsOptions>()
            .BindConfiguration(MobilePushEndpointsOptions.SectionName)
            .ValidateOnStart();
}
