using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Presence.Notifications;

/// <summary>
/// Granit module that bridges <c>Granit.Presence</c> with <c>Granit.Notifications</c>.
/// Registers <see cref="PresenceNotificationDeliveryGate"/> so push notifications are
/// suppressed for users in <c>DoNotDisturb</c> or <c>Offline</c>.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitPresenceModule))]
public sealed class GranitPresenceNotificationsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Scoped: PresenceNotificationDeliveryGate depends on IPresenceQueryService (Scoped).
        // Registering the gate as Singleton would either throw under ValidateScopes=true or
        // silently capture the root-scope query service forever — a captive-dependency bug.
        // NotificationFanoutHandler resolves IEnumerable<INotificationDeliveryGate> per call,
        // so a Scoped gate is honored without instantiation overhead concerns.
        context.Services.AddScoped<INotificationDeliveryGate, PresenceNotificationDeliveryGate>();
    }
}
