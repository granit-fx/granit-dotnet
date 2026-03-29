using Granit.Modularity;

namespace Granit.Notifications.Sms;

/// <summary>
/// Granit module for the SMS notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsSms()</c>.
/// Providers register keyed <c>ISmsSender</c> implementations.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsSmsModule : GranitModule;
