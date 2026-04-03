using Granit.Modularity;
using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>
/// Notification types for subscription lifecycle events: trial expiration,
/// plan changes, cancellation, suspension, and renewal.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsNotificationsModule : GranitModule;
