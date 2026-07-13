using Granit.Modularity;
using Granit.Notifications.AzureNotificationHubs.Extensions;
using Granit.Notifications.MobilePush;

namespace Granit.Notifications.AzureNotificationHubs;

/// <summary>
/// Granit module for the Azure Notification Hubs mobile push provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsAzureNotificationHubs()</c>.
/// Registers <c>AzureNotificationHubsPushSender</c> as a keyed <c>IMobilePushSender</c> implementation.
/// </remarks>
[DependsOn(typeof(GranitNotificationsMobilePushModule))]
public sealed class GranitNotificationsAzureNotificationHubsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsAzureNotificationHubs();
}
