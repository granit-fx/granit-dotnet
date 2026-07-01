using Granit.Modularity;

namespace Granit.Notifications.WebPush;

/// <summary>
/// Granit module for the W3C Web Push (VAPID) notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsWebPush()</c>.
/// Registers <c>WebPushNotificationChannel</c> for browser push notifications.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsWebPushModule : GranitModule;
