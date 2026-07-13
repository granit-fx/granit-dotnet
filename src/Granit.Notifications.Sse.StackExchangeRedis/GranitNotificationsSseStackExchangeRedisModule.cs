using Granit.Modularity;
using Granit.Notifications.Sse.StackExchangeRedis.Extensions;

namespace Granit.Notifications.Sse.StackExchangeRedis;

/// <summary>
/// Granit module for the SSE Redis backplane — referencing it makes SSE delivery
/// multi-replica safe (publish via Redis pub/sub, deliver locally on every node).
/// </summary>
[DependsOn(typeof(GranitNotificationsSseModule))]
public sealed class GranitNotificationsSseStackExchangeRedisModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsSseRedisBackplane();
}
