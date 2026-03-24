using Granit.Modularity;
using Granit.Templating.Workflow.Extensions;
using Granit.Workflow;

namespace Granit.Templating.Workflow;

/// <summary>
/// Granit module bridging <c>Granit.Templating</c> and <c>Granit.Workflow</c>.
/// Replaces the default lifecycle management with Workflow FSM validation,
/// approval routing, and unified ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// Register via <see cref="ServiceCollectionExtensions.AddGranitTemplatingWorkflow"/>
/// from the host application.
/// </remarks>
[DependsOn(
    typeof(GranitTemplatingModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitTemplatingWorkflowModule : GranitModule;
