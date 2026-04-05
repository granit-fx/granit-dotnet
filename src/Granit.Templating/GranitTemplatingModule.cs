using Granit.Modularity;
using Granit.Templating.Extensions;
using Granit.Timing;
using Granit.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating;

/// <summary>
/// Granit module for generic text templating.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
[DependsOn(typeof(GranitWorkflowModule))]
public sealed class GranitTemplatingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTemplating();
}
