using Granit.Modularity;
using Granit.Notifications.MobilePush.AzureNotificationHubs.Extensions;

namespace Granit.Notifications.MobilePush.AzureNotificationHubs;

/// <summary>
/// Granit module for the Azure Notification Hubs mobile push provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsMobilePushAzureNotificationHubs()</c>.
/// Registers <c>AzureNotificationHubsPushSender</c> as a keyed <c>IMobilePushSender</c> implementation.
/// </remarks>
[DependsOn(typeof(GranitNotificationsMobilePushModule))]
public sealed class GranitNotificationsMobilePushAzureNotificationHubsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsMobilePushAzureNotificationHubs();
}
