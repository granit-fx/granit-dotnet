using Granit.Modularity;

namespace Granit.Notifications.WebPush;

/// <summary>
/// Granit module for the W3C Web Push (VAPID) notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsPush()</c>.
/// Registers <c>PushNotificationChannel</c> for browser push notifications.
/// </remarks>
[DependsOn(typeof(GranitNotificationsModule))]
public sealed class GranitNotificationsWebPushModule : GranitModule;
