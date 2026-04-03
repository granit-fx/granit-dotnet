using Granit.Modularity;
using Granit.Notifications;

namespace Granit.Payments.Notifications;

/// <summary>
/// Notification types for payment events: success, failure, method expiring,
/// refund processed, and dispute opened.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitPaymentsModule))]
public sealed class GranitPaymentsNotificationsModule : GranitModule;
