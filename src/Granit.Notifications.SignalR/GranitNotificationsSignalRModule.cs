using Granit.Modularity;

namespace Granit.Notifications.SignalR;

/// <summary>
/// Granit module for the SignalR real-time notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsSignalR()</c>.
/// Registers <c>SignalRNotificationChannel</c> and the associated SignalR hub infrastructure.
/// </remarks>
[DependsOn(typeof(GranitNotificationsModule))]
public sealed class GranitNotificationsSignalRModule : GranitModule;
