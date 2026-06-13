using Granit.AI.Tools.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Tools;

/// <summary>
/// Granit module for the agentic AI tool seam (ADR-067). Registers the
/// <see cref="IAIToolRegistry"/> and <see cref="IAIToolProjector"/> so an application can
/// expose a curated, ACL-bound set of tools to the chat orchestrator.
/// </summary>
/// <remarks>
/// Tools are opted in by application code via
/// <see cref="AIToolsServiceCollectionExtensions.AddGranitAITools(IServiceCollection, System.Action{AIToolRegistrationBuilder})"/>,
/// not by a framework-wide attribute. This module only wires the registry and projector;
/// it adds no tools of its own. The base module is dependency-free — the seam needs only
/// the module system and <c>Microsoft.Extensions.AI.Abstractions</c>.
/// </remarks>
public sealed class GranitAIToolsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAITools();
}
