using Granit.Modularity;
using Granit.Notifications.MobilePush.Diagnostics;
using Granit.Notifications.MobilePush.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.MobilePush;

/// <summary>
/// Granit module for the mobile push notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsMobilePush()</c>.
/// Providers (FCM, APNs) register keyed <c>IMobilePushSender</c> implementations.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsMobilePushModule : GranitModule
{
    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        IHostEnvironment environment = context.ServiceProvider.GetRequiredService<IHostEnvironment>();

        if (!environment.IsDevelopment() &&
            context.ServiceProvider.GetRequiredService<IMobilePushTokenReader>() is InMemoryMobilePushTokenStore)
        {
            MobilePushLog.InMemoryStoreActiveInNonDevelopment(
                context.ServiceProvider.GetRequiredService<ILogger<GranitNotificationsMobilePushModule>>());
        }
    }
}
