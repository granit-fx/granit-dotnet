using Granit.AI.Internal;
using Granit.Authorization;
using Granit.Guids;
using Granit.Modularity;
using Granit.Settings;
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
/// <para>
/// Depends on <c>Granit.Settings</c> for per-tenant credential storage via the cascade
/// in <c>Granit.AI.Tenancy.IAIProviderCredentialResolver</c>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitGuidsModule))]
[DependsOn(typeof(GranitAuthorizationModule))]
[DependsOn(typeof(GranitSettingsModule))]
public sealed class GranitAIModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IAIChatCompletionService, DefaultAIChatCompletionService>();
}
