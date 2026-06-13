using Granit.AI.Tools.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Tools;

/// <summary>
/// Granit module for the agentic AI tool seam and orchestration loop (ADR-067). Registers the
/// <see cref="IAIToolRegistry"/>, <see cref="IAIToolProjector"/> and
/// <see cref="IAIToolOrchestrator"/> so an application can expose a curated, ACL-bound set of
/// tools to the chat agent and drive a bounded think → call → execute loop over them.
/// </summary>
/// <remarks>
/// Tools are opted in by application code via
/// <see cref="AIToolsServiceCollectionExtensions.AddGranitAITools(IServiceCollection, System.Action{AIToolRegistrationBuilder})"/>,
/// not by a framework-wide attribute. This module wires the registry, projector and
/// orchestrator; it adds no tools of its own. It depends on <c>Granit.AI</c> for the chat
/// client and usage tracking the loop drives.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIToolsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAITools();
}
