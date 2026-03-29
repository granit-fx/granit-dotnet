using Granit.Modularity;

namespace Granit.Notifications;

/// <summary>
/// Granit module for notification contracts (INotificationPublisher, INotificationChannel,
/// NotificationType, domain entities).
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so that consumer modules
/// can declare <c>[DependsOn(typeof(GranitNotificationsAbstractionsModule))]</c>
/// without pulling in the full notification engine implementation.
/// </remarks>
public sealed class GranitNotificationsAbstractionsModule : GranitModule;
