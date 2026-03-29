using Granit.Modularity;

namespace Granit.Notifications.Sse;

/// <summary>
/// Granit module for the Server-Sent Events (SSE) notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsSse()</c>.
/// Registers <c>SseConnectionManager</c> and <c>SseNotificationChannel</c>.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsSseModule : GranitModule;
