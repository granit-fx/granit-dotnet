using Granit.AI.Internal;
using Granit.Guids;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI;

/// <summary>
/// Granit module for AI services (provider-agnostic core).
/// </summary>
/// <remarks>
/// Defines the <see cref="Workspaces.IAIWorkspaceProvider"/>,
/// <see cref="IAIChatClientFactory"/>, and <see cref="IAIUsageTracker"/> abstractions.
/// Register a concrete provider (e.g. <c>Granit.AI.OpenAI</c>) and a
/// persistence adapter (e.g. <c>Granit.AI.EntityFrameworkCore</c>) alongside this module.
/// <para>
/// Built on <c>Microsoft.Extensions.AI</c> (<c>IChatClient</c>, <c>IEmbeddingGenerator</c>).
/// Providers are interchangeable per environment (e.g. Ollama in dev, Azure OpenAI in prod).
/// </para>
/// <para>
/// Localization resources (<c>Localization/AI/{culture}.json</c>) are embedded in this
/// assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="AILocalizationResource"/>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitGuidsModule))]
public sealed class GranitAIModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IAIChatCompletionService, DefaultAIChatCompletionService>();
}
