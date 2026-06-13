using Granit.AI.Tools.Diagnostics;
using Granit.AI.Tools.Internal;
using Granit.AI.Tools.Options;
using Granit.AI.Tools.Prompts;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Tools.Extensions;

/// <summary>
/// Application-facing registration for the Granit AI tool seam.
/// </summary>
public static class AIToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AI tool registry and projector and opens the opt-in builder so the
    /// application can expose a curated, ACL-safe set of tools to the chat agent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Opt-in tool registrations.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitAITools(
        this IServiceCollection services,
        Action<AIToolRegistrationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddCoreServices(services);
        configure(new AIToolRegistrationBuilder(services));
        return services;
    }

    /// <summary>
    /// Registers the AI tool registry and projector without any tools. Tools can be added
    /// later via <see cref="AddGranitAITools(IServiceCollection, Action{AIToolRegistrationBuilder})"/>
    /// or by registering <see cref="IAITool"/> implementations directly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitAITools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddCoreServices(services);
        return services;
    }

    private static void AddCoreServices(IServiceCollection services)
    {
        services.AddOptions<GranitAIToolsOrchestrationOptions>()
            .BindConfiguration(GranitAIToolsOrchestrationOptions.SectionName);

        services.TryAddScoped<IAIToolRegistry, AIToolRegistry>();
        services.TryAddScoped<IAIToolProjector, AIToolProjector>();
        services.TryAddScoped<IAIToolAuthorizer, PermissionAIToolAuthorizer>();
        services.TryAddScoped<IAIToolOrchestrator, AIToolOrchestrator>();
        services.TryAddSingleton<IAIGuardrailProvider, DefaultAIGuardrailProvider>();
        services.TryAddSingleton<IAISystemPromptComposer, DefaultAISystemPromptComposer>();
        services.TryAddSingleton<AIToolsMetrics>();
        services.TryAddSingleton(TimeProvider.System);

        GranitActivitySourceRegistry.Register(AIToolsActivitySource.Name);
    }
}
