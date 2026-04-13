using System.Diagnostics.CodeAnalysis;
using Granit.AI.Diagnostics;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit AI core services.
/// </summary>
/// <remarks>Not dead code — called by host applications to register AI core services.</remarks>
[ExcludeFromCodeCoverage]
public static class AIServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit AI core services: workspace management, chat client factory, and usage tracking.
    /// </summary>
    /// <remarks>
    /// This registers the provider-agnostic infrastructure. You must also register at least one
    /// provider (e.g. <c>AddGranitAIOpenAI()</c>) for the factories to resolve clients.
    /// Without a persistence adapter (<c>Granit.AI.EntityFrameworkCore</c>), null implementations
    /// are used for workspace storage and usage tracking (graceful degradation).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIActivitySource.Name);

        builder.Services
            .AddOptions<GranitAIOptions>()
            .BindConfiguration(GranitAIOptions.SectionName);

        // Workspace infrastructure
        builder.Services.TryAddSingleton<IAIWorkspaceProvider, DefaultAIWorkspaceProvider>();
        builder.Services.TryAddSingleton<IAIWorkspaceStoreReader, NullAIWorkspaceStoreReader>();
        builder.Services.TryAddSingleton<IAIWorkspaceStoreWriter, NullAIWorkspaceStoreWriter>();

        // Factories
        builder.Services.TryAddSingleton<IAIChatClientFactory, DefaultAIChatClientFactory>();
        builder.Services.TryAddSingleton<IAIEmbeddingGeneratorFactory, DefaultAIEmbeddingGeneratorFactory>();

        // Metrics
        builder.Services.TryAddSingleton<AIMetrics>();

        // Usage tracking (no-op by default, overridden by EF Core package)
        builder.Services.TryAddSingleton<IAIUsageTracker, NullAIUsageTracker>();

        // Quota guard: InMemory by default (no-op when MaxRequestsPerTenantPerHour=0)
        builder.Services
            .AddOptions<Options.AIQuotaOptions>()
            .BindConfiguration(Options.AIQuotaOptions.SectionName);

        builder.Services.TryAddSingleton<IAIQuotaGuard, InMemoryAIQuotaGuard>();

        return builder;
    }
}
