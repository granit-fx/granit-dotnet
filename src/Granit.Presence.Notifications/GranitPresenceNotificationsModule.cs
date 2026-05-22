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
    typeof(GranitPresenceModule),
    typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitPresenceNotificationsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.AddSingleton<INotificationDeliveryGate, PresenceNotificationDeliveryGate>();
    }
}
