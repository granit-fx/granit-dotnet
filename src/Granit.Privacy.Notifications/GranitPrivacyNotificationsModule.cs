using Granit.Modularity;
using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Granit module for the privacy notification bridge.
/// Routes deletion reminder and confirmation events to users via <c>Granit.Notifications</c>.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsModule),
    typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyNotificationsModule : GranitModule;
