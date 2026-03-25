using Granit.Guids;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Timeline.Extensions;
using Granit.Timing;
using Granit.Users;

namespace Granit.Timeline;

/// <summary>
/// Granit module for the unified activity stream engine.
/// </summary>
/// <remarks>
/// Default registrations use in-memory stores suitable for development and tests.
/// For production, call <c>AddGranitTimelineEntityFrameworkCore()</c>
/// to enable durable persistence.
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitQueryEngineModule),
    typeof(GranitTimingModule))]
public sealed class GranitTimelineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTimeline();
}
