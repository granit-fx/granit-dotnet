using Granit.Modularity;
using Granit.Scheduling;
using Granit.Workflow.Scheduling.Extensions;

namespace Granit.Workflow.Scheduling;

/// <summary>
/// Granit module that bridges <c>Granit.Workflow</c> and <c>Granit.Scheduling</c>: registers the
/// scheduled-workflow-transition service and applier registry, and contributes the
/// <see cref="ScheduledWorkflowTransitionHandler"/> to Wolverine handler discovery.
/// </summary>
/// <remarks>
/// A durable <c>IScheduler</c> provider (e.g. <c>Granit.Scheduling.Wolverine</c>) must also be
/// registered by the host for scheduled transitions to be delivered. The
/// <c>ScheduledActionStatusMiddleware</c> from that provider automatically wraps this handler.
/// </remarks>
[DependsOn(
    typeof(GranitWorkflowModule),
    typeof(GranitSchedulingModule))]
public sealed class GranitWorkflowSchedulingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddWorkflowScheduling();
}
