using Granit.Guids;
using Granit.Modularity;
using Granit.Notifications.Extensions;
using Granit.QueryEngine;
using Granit.Timing;

namespace Granit.Notifications;

/// <summary>
/// Granit module for the multi-channel notification engine.
/// </summary>
/// <remarks>
/// Default registrations use in-memory stores and in-process channel dispatch,
/// suitable for development and tests. For production, add
/// <c>Granit.Notifications.Wolverine</c> for durable outbox dispatch and call
/// <c>AddGranitNotificationsEntityFrameworkCore()</c> for persistent stores.
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitQueryEngineAbstractionsModule),
    typeof(GranitTimingModule))]
public sealed class GranitNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitNotifications();
}
