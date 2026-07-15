using Granit.AI.Diagnostics;
using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Internal;

/// <summary>
/// Default factory that resolves an <see cref="IEmbeddingGenerator{String, Embedding}"/> by
/// workspace name and wraps it in the usage-tracking middleware
/// (<see cref="UsageTrackingEmbeddingGenerator"/>), so every generation stamps an
/// <see cref="AIUsageRecord"/> without caller involvement.
/// </summary>
internal sealed class DefaultAIEmbeddingGeneratorFactory(
    IAIWorkspaceProvider workspaceProvider,
    IEnumerable<IAIProviderFactory> providerFactories,
    IAIUsageTracker usageTracker,
    IAIUsageRecordFactory usageRecordFactory,
    AIMetrics metrics,
    TimeProvider timeProvider,
    IOptions<GranitAIOptions> options) : IAIEmbeddingGeneratorFactory
{
    private readonly Dictionary<string, IAIProviderFactory> _providers =
        providerFactories.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateAsync(
        string? workspaceName = null,
        CancellationToken cancellationToken = default)
    {
        string name = workspaceName ?? options.Value.DefaultWorkspace;

        AIWorkspace workspace = await workspaceProvider.GetAsync(name, cancellationToken).ConfigureAwait(false)
            ?? throw new AIWorkspaceNotFoundException(name);

        if (!workspace.Activated)
        {
            throw new AIWorkspaceNotActiveException(name);
        }

        if (!_providers.TryGetValue(workspace.Provider, out IAIProviderFactory? providerFactory))
        {
            throw new AIProviderNotRegisteredException(workspace.Provider);
        }

        IEmbeddingGenerator<string, Embedding<float>>? generator =
            await providerFactory.CreateEmbeddingGeneratorAsync(workspace, cancellationToken).ConfigureAwait(false);

        if (generator is null)
        {
            throw new InvalidOperationException(
                $"Provider '{workspace.Provider}' does not support embedding generation.");
        }

        return new UsageTrackingEmbeddingGenerator(
            generator, name, workspace, usageTracker, usageRecordFactory, metrics, timeProvider);
    }
}
