using Granit.Core.Modularity;
using Granit.Observability.Extensions;

namespace Granit.Observability;

/// <summary>
/// Granit module for Serilog + OpenTelemetry.
/// Uses context.Builder because Serilog requires IHostApplicationBuilder.
/// </summary>
public sealed class GranitObservabilityModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitObservability();
}
