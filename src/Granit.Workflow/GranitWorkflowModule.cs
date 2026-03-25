using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Timing;
using Granit.Workflow.Extensions;

namespace Granit.Workflow;

/// <summary>
/// Granit module for the workflow FSM engine.
/// Provides generic state machine definitions, transition management with approval routing,
/// and domain events for state changes.
/// </summary>
[DependsOn(
    typeof(GranitQueryEngineModule),
    typeof(GranitTimingModule))]
public sealed class GranitWorkflowModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitWorkflow();
}
