using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Internal;

/// <summary>
/// Default factory that resolves an <see cref="IEmbeddingGenerator{String, Embedding}"/> by workspace name.
/// </summary>
internal sealed class DefaultAIEmbeddingGeneratorFactory(
    IAIWorkspaceProvider workspaceProvider,
    IEnumerable<IAIProviderFactory> providerFactories,
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

        return generator
            ?? throw new InvalidOperationException(
                $"Provider '{workspace.Provider}' does not support embedding generation.");
    }
}
