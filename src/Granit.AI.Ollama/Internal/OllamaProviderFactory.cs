using Granit.AI.Ollama.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;

namespace Granit.AI.Ollama.Internal;

/// <summary>
/// Ollama implementation of <see cref="IAIProviderFactory"/> and <see cref="IAIModelCatalog"/>.
/// Creates <see cref="OllamaApiClient"/> instances that implement both
/// <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>.
/// </summary>
/// <remarks>
/// <see cref="OllamaApiClient"/> natively supports the <c>Microsoft.Extensions.AI</c>
/// abstractions. Each call creates a new client pointing at the configured
/// endpoint with the workspace model (or the default model from options).
/// Model catalog is fetched dynamically via <c>GET /api/tags</c> and cached for 30 seconds.
/// </remarks>
internal sealed class OllamaProviderFactory(
    IOptions<OllamaOptions> options,
    TimeProvider timeProvider) : IAIProviderFactory, IAIModelCatalog
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private readonly Lock _lock = new();
    private IReadOnlyList<AIModelInfo>? _cachedModels;
    private DateTimeOffset _cacheExpiry;

    /// <inheritdoc/>
    public string ProviderName => "Ollama";

    /// <inheritdoc/>
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        string model = workspace.Model ?? options.Value.DefaultModel;
        var endpoint = new Uri(options.Value.Endpoint);

        return new OllamaApiClient(endpoint, model);
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        string model = workspace.Model ?? options.Value.DefaultModel;
        var endpoint = new Uri(options.Value.Endpoint);

        return new OllamaApiClient(endpoint, model);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cachedModels is not null && timeProvider.GetUtcNow() < _cacheExpiry)
            {
                return _cachedModels;
            }
        }

        var endpoint = new Uri(options.Value.Endpoint);
        var client = new OllamaApiClient(endpoint);

        IEnumerable<Model> localModels = await client
            .ListLocalModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AIModelInfo> models = localModels
            .Select(m => new AIModelInfo(
                m.Name,
                m.Name,
                new AIModelCapabilities(Chat: true, Embeddings: true)))
            .ToList();

        lock (_lock)
        {
            _cachedModels = models;
            _cacheExpiry = timeProvider.GetUtcNow().Add(CacheDuration);
        }

        return models;
    }
}
