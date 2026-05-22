using Granit.Caching;
using Granit.Guids;
using Granit.Modularity;
using Granit.Presence.Extensions;
using Granit.Timing;

namespace Granit.Presence;

/// <summary>
/// Granit module for user presence and availability tracking.
/// </summary>
/// <remarks>
/// <para>
/// Default registrations use an in-memory override store and a FusionCache-backed
/// heartbeat tracker (single-pod). For production, add
/// <c>Granit.Presence.EntityFrameworkCore</c> for persistence and
/// <c>Granit.Caching.StackExchangeRedis</c> for the Redis L2 backplane so
/// heartbeats propagate across pods.
/// </para>
/// <para>
/// To connect presence with notification suppression in <c>DoNotDisturb</c>, load
/// the <c>Granit.Presence.Notifications</c> companion package which registers a
/// notification delivery gate filtering push channels.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitCachingModule),
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule))]
public sealed class GranitPresenceModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Builder.AddGranitPresence();
    }
}
