using Granit.Modularity;
using Granit.Notifications.WebPush.Diagnostics;
using Granit.Notifications.WebPush.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.WebPush;

/// <summary>
/// Granit module for the W3C Web Push (VAPID) notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsWebPush()</c>.
/// Registers <c>WebPushNotificationChannel</c> for browser push notifications.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsWebPushModule : GranitModule
{
    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        IHostEnvironment environment = context.ServiceProvider.GetRequiredService<IHostEnvironment>();

        if (!environment.IsDevelopment() &&
            context.ServiceProvider.GetRequiredService<IWebPushSubscriptionReader>() is InMemoryWebPushSubscriptionStore)
        {
            WebPushLog.InMemoryStoreActiveInNonDevelopment(
                context.ServiceProvider.GetRequiredService<ILogger<GranitNotificationsWebPushModule>>());
        }
    }
}
