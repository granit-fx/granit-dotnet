using Granit.Guids;
using Granit.Modularity;
using Granit.Scheduling.Extensions;
using Granit.Timing;

namespace Granit.Scheduling;

/// <summary>
/// Granit module for one-shot scheduled actions (provider-agnostic core).
/// </summary>
/// <remarks>
/// Provides <see cref="IScheduler"/>, <see cref="IScheduledPayload"/>, and the
/// <see cref="Domain.ScheduledAction"/> aggregate root. For durable scheduling,
/// add <c>Granit.Scheduling.Wolverine</c>.
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule))]
public sealed class GranitSchedulingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitScheduling();
}
