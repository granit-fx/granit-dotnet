using Granit.Modularity;

namespace Granit.Notifications.MobilePush;

/// <summary>
/// Granit module for the mobile push notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsMobilePush()</c>.
/// Providers (FCM, APNs) register keyed <c>IMobilePushSender</c> implementations.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsMobilePushModule : GranitModule;
