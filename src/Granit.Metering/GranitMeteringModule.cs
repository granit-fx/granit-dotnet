using Granit.Metering.Extensions;
using Granit.Modularity;
using Granit.Timing;
using Granit.Workflow;

namespace Granit.Metering;

/// <summary>
/// Granit module for usage-based metering (event recording, aggregation, quota enforcement).
/// </summary>
/// <remarks>
/// Provides the domain model, CQRS interfaces, and integration events.
/// Add <c>Granit.Metering.EntityFrameworkCore</c> for persistence and
/// <c>Granit.Metering.BackgroundJobs</c> for automated aggregation.
/// </remarks>
[DependsOn(
    typeof(GranitTimingModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitMeteringModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitMetering();
}
