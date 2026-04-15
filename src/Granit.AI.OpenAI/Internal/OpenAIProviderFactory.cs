using System.ClientModel;
using System.Collections.Frozen;
using Granit.AI.OpenAI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Models;

namespace Granit.AI.OpenAI.Internal;

/// <summary>
/// OpenAI implementation of <see cref="IAIProviderFactory"/> and <see cref="IAIModelCatalog"/>.
/// </summary>
/// <remarks>
/// Creates <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{String, Embedding}"/>
/// instances backed by the OpenAI API via <see cref="OpenAIClient"/>.
/// Model catalog is fetched dynamically from the OpenAI <c>GET /v1/models</c> endpoint
/// and enriched with known metadata (context window, capabilities) for well-known models.
/// Results are cached for 5 minutes.
/// </remarks>
internal sealed class OpenAIProviderFactory(
    IOptions<OpenAIProviderOptions> options,
    TimeProvider timeProvider) : IAIProviderFactory, IAIModelCatalog
{
    private readonly OpenAIProviderOptions _options = options.Value;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly Lock _lock = new();
    private IReadOnlyList<AIModelInfo>? _cachedModels;
    private DateTimeOffset _cacheExpiry;

    /// <summary>
    /// Known metadata for well-known OpenAI models. Models not in this map
    /// get default chat capabilities with no context window info.
    /// </summary>
    private static readonly FrozenDictionary<string, (string DisplayName, AIModelCapabilities Capabilities, int? MaxContextTokens)> KnownModels =
        new Dictionary<string, (string, AIModelCapabilities, int?)>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o"] = ("GPT-4o", new AIModelCapabilities(Chat: true, Embeddings: false), 128_000),
            ["gpt-4o-mini"] = ("GPT-4o Mini", new AIModelCapabilities(Chat: true, Embeddings: false), 128_000),
            ["gpt-4.1"] = ("GPT-4.1", new AIModelCapabilities(Chat: true, Embeddings: false), 1_047_576),
            ["gpt-4.1-mini"] = ("GPT-4.1 Mini", new AIModelCapabilities(Chat: true, Embeddings: false), 1_047_576),
            ["gpt-4.1-nano"] = ("GPT-4.1 Nano", new AIModelCapabilities(Chat: true, Embeddings: false), 1_047_576),
            ["o3"] = ("o3", new AIModelCapabilities(Chat: true, Embeddings: false), 200_000),
            ["o3-mini"] = ("o3 Mini", new AIModelCapabilities(Chat: true, Embeddings: false), 200_000),
            ["o4-mini"] = ("o4 Mini", new AIModelCapabilities(Chat: true, Embeddings: false), 200_000),
            ["text-embedding-3-small"] = ("Text Embedding 3 Small", new AIModelCapabilities(Chat: false, Embeddings: true), 8_191),
            ["text-embedding-3-large"] = ("Text Embedding 3 Large", new AIModelCapabilities(Chat: false, Embeddings: true), 8_191),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string ProviderName => "OpenAI";

    /// <inheritdoc/>
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        OpenAIClient client = CreateOpenAIClient();
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? _options.DefaultModel : workspace.Model;

        return client.GetChatClient(model).AsIChatClient();
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        OpenAIClient client = CreateOpenAIClient();

        return client.GetEmbeddingClient(_options.DefaultEmbeddingModel).AsIEmbeddingGenerator();
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

        OpenAIClient client = CreateOpenAIClient();
        OpenAIModelClient modelClient = client.GetOpenAIModelClient();

        OpenAIModelCollection apiModels = await modelClient
            .GetModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AIModelInfo> models = apiModels
            .Select(m => EnrichModel(m.Id))
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_lock)
        {
            _cachedModels = models;
            _cacheExpiry = timeProvider.GetUtcNow().Add(CacheDuration);
        }

        return models;
    }

    private static AIModelInfo EnrichModel(string modelId)
    {
        if (KnownModels.TryGetValue(modelId, out (string DisplayName, AIModelCapabilities Capabilities, int? MaxContextTokens) known))
        {
            return new AIModelInfo(modelId, known.DisplayName, known.Capabilities, known.MaxContextTokens);
        }

        AIModelCapabilities capabilities = modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase)
            ? new AIModelCapabilities(Chat: false, Embeddings: true)
            : new AIModelCapabilities(Chat: true, Embeddings: false);

        return new AIModelInfo(modelId, modelId, capabilities);
    }

    private OpenAIClient CreateOpenAIClient()
    {
        var credential = new ApiKeyCredential(_options.ApiKey);

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(_options.Endpoint) };
            return new OpenAIClient(credential, clientOptions);
        }

        return new OpenAIClient(credential);
    }
}
