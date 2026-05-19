using System.Collections.Frozen;
using Granit.AI.OpenAI.Options;
using Granit.AI.Tenancy;
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
/// <para>
/// Cascade Workspace &#8594; Tenant Setting &#8594; Global Setting &#8594; Host Options resolved
/// by <see cref="OpenAICredentialResolver"/>. SDK clients are reused across requests with the
/// same fingerprint via the Singleton <see cref="OpenAIClientCache"/>.
/// </para>
/// <para>
/// Model catalog (<see cref="GetAvailableModelsAsync"/>) is queried with the Host-options client
/// (no tenant context) — Anthropic-style per-tenant catalog views were considered but rejected
/// for this PR: the catalog rarely differs across credentials inside a given organisation.
/// </para>
/// </remarks>
internal sealed class OpenAIProviderFactory(
    IOptionsMonitor<OpenAIProviderOptions> optionsMonitor,
    OpenAICredentialResolver credentialResolver,
    OpenAIClientCache clientCache,
    TimeProvider timeProvider) : IAIProviderFactory, IAIModelCatalog
{
    /// <summary>Named <see cref="HttpClient"/> consumed by the SDK client cache.</summary>
    internal const string HttpClientName = "Granit.AI.OpenAI";

    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Known metadata for well-known OpenAI models. Models not in this map get default chat
    /// capabilities with no context window info.
    /// </summary>
    private static readonly FrozenDictionary<string, (string DisplayName, AIModelCapabilities Capabilities, int? MaxContextTokens)> KnownModels =
        new Dictionary<string, (string, AIModelCapabilities, int?)>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o"] = ("GPT-4o", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 128_000),
            ["gpt-4o-mini"] = ("GPT-4o Mini", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 128_000),
            ["gpt-4.1"] = ("GPT-4.1", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 1_047_576),
            ["gpt-4.1-mini"] = ("GPT-4.1 Mini", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 1_047_576),
            ["gpt-4.1-nano"] = ("GPT-4.1 Nano", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 1_047_576),
            ["o3"] = ("o3", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 200_000),
            ["o3-mini"] = ("o3 Mini", new AIModelCapabilities { ToolUse = true, StructuredOutput = true }, 200_000),
            ["o4-mini"] = ("o4 Mini", new AIModelCapabilities { Vision = true, ToolUse = true, StructuredOutput = true }, 200_000),
            ["text-embedding-3-small"] = ("Text Embedding 3 Small", new AIModelCapabilities { Chat = false, Embeddings = true, Streaming = false }, 8_191),
            ["text-embedding-3-large"] = ("Text Embedding 3 Large", new AIModelCapabilities { Chat = false, Embeddings = true, Streaming = false }, 8_191),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _catalogLock = new();
    private IReadOnlyList<AIModelInfo>? _cachedModels;
    private DateTimeOffset _cacheExpiry;

    /// <inheritdoc/>
    public string ProviderName => "OpenAI";

    /// <inheritdoc/>
    public async ValueTask<IChatClient> CreateChatClientAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OpenAIProviderOptions opts = optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? opts.DefaultModel : workspace.Model;
        EnforceAllowlist(opts, model);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        OpenAIClient sdk = clientCache.GetOrCreate(credential.ApiKey!, credential.Endpoint);
        IChatClient inner = sdk.GetChatClient(model).AsIChatClient();
        return new TracingOpenAIChatClient(inner, model, credential);
    }

    /// <inheritdoc/>
    public async ValueTask<IEmbeddingGenerator<string, Embedding<float>>?> CreateEmbeddingGeneratorAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OpenAIProviderOptions opts = optionsMonitor.CurrentValue;
        string model = opts.DefaultEmbeddingModel;
        EnforceAllowlist(opts, model);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        OpenAIClient sdk = clientCache.GetOrCreate(credential.ApiKey!, credential.Endpoint);
        return sdk.GetEmbeddingClient(model).AsIEmbeddingGenerator();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        lock (_catalogLock)
        {
            if (_cachedModels is not null && timeProvider.GetUtcNow() < _cacheExpiry)
            {
                return _cachedModels;
            }
        }

        OpenAIProviderOptions opts = optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            return [];
        }

        OpenAIClient client = clientCache.GetOrCreate(opts.ApiKey, opts.Endpoint);
        OpenAIModelClient modelClient = client.GetOpenAIModelClient();

        OpenAIModelCollection apiModels = await modelClient
            .GetModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AIModelInfo> models = apiModels
            .Select(m => EnrichModel(m.Id))
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_catalogLock)
        {
            _cachedModels = models;
            _cacheExpiry = timeProvider.GetUtcNow().Add(CatalogCacheDuration);
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
            ? new AIModelCapabilities { Chat = false, Embeddings = true, Streaming = false }
            : new AIModelCapabilities();

        return new AIModelInfo(modelId, modelId, capabilities);
    }

    private static void EnforceAllowlist(OpenAIProviderOptions options, string model)
    {
        if (options.AllowedModels.Count > 0 &&
            !options.AllowedModels.Contains(model, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"OpenAI model '{model}' is not in the configured AllowedModels list. " +
                "Add it to AI:OpenAI:AllowedModels or update the workspace.");
        }
    }
}
