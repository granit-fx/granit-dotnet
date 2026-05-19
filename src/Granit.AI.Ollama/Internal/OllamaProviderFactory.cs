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
/// <para>
/// <see cref="OllamaApiClient"/> natively supports the <c>Microsoft.Extensions.AI</c>
/// abstractions. The chat client is wrapped by <see cref="TracingOllamaChatClient"/>
/// so each call emits an OpenTelemetry GenAI span.
/// </para>
/// <para>
/// The transport <see cref="HttpClient"/> is sourced from <see cref="IHttpClientFactory"/>,
/// so connection pooling, DNS refresh, and timeout enforcement are owned by the host.
/// Endpoint changes are picked up via <see cref="IOptionsMonitor{TOptions}"/>; consecutive
/// calls observe the new endpoint without a process restart.
/// </para>
/// <para>
/// Model catalog is fetched dynamically via <c>GET /api/tags</c> and enriched with
/// per-model capabilities via <c>GET /api/show</c>. Cached for 30 seconds.
/// </para>
/// </remarks>
internal sealed class OllamaProviderFactory : IAIProviderFactory, IAIModelCatalog
{
    /// <summary>Named <see cref="HttpClient"/> consumed by this factory.</summary>
    internal const string HttpClientName = "Granit.AI.Ollama";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IOptionsMonitor<OllamaProviderOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private IReadOnlyList<AIModelInfo>? _cachedModels;
    private DateTimeOffset _cacheExpiry;

    public OllamaProviderFactory(
        IOptionsMonitor<OllamaProviderOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider)
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public string ProviderName => "Ollama";

    /// <inheritdoc/>
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OllamaProviderOptions options = _optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? options.DefaultModel : workspace.Model;
        EnforceAllowlist(options, model);

        OllamaApiClient inner = CreateOllamaApiClient(options, model);
        return new TracingOllamaChatClient(inner, model);
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OllamaProviderOptions options = _optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? options.DefaultModel : workspace.Model;
        EnforceAllowlist(options, model);

        return CreateOllamaApiClient(options, model);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cachedModels is not null && _timeProvider.GetUtcNow() < _cacheExpiry)
            {
                return _cachedModels;
            }
        }

        OllamaProviderOptions options = _optionsMonitor.CurrentValue;
        OllamaApiClient client = CreateOllamaApiClient(options, options.DefaultModel);

        IEnumerable<Model> localModels = await client
            .ListLocalModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        AIModelInfo[] models = await Task.WhenAll(localModels.Select(async model =>
        {
            AIModelCapabilities capabilities = await ResolveCapabilitiesAsync(client, model.Name, cancellationToken)
                .ConfigureAwait(false);
            return new AIModelInfo(model.Name, model.Name, capabilities);
        })).ConfigureAwait(false);

        lock (_lock)
        {
            _cachedModels = models;
            _cacheExpiry = _timeProvider.GetUtcNow().Add(CacheDuration);
        }

        return models;
    }

    private static void EnforceAllowlist(OllamaProviderOptions options, string model)
    {
        if (options.AllowedModels.Count > 0 &&
            !options.AllowedModels.Contains(model, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Ollama model '{model}' is not in the configured AllowedModels list. " +
                "Add it to AI:Ollama:AllowedModels or update the workspace.");
        }
    }

    private OllamaApiClient CreateOllamaApiClient(OllamaProviderOptions options, string model)
    {
        HttpClient http = _httpClientFactory.CreateClient(HttpClientName);
        http.BaseAddress = new Uri(options.Endpoint);
        http.Timeout = options.Timeout;
        return new OllamaApiClient(http, model);
    }

    /// <summary>
    /// Resolves capabilities for a specific model via <c>GET /api/show</c>.
    /// Ollama returns capabilities as a string list (e.g. <c>completion</c>, <c>vision</c>, <c>tools</c>, <c>embedding</c>).
    /// </summary>
    private static async Task<AIModelCapabilities> ResolveCapabilitiesAsync(
        OllamaApiClient client,
        string modelName,
        CancellationToken cancellationToken)
    {
        try
        {
            ShowModelResponse details = await client
                .ShowModelAsync(new ShowModelRequest { Model = modelName }, cancellationToken)
                .ConfigureAwait(false);

            IEnumerable<string>? caps = details.Capabilities;

            if (caps is null)
            {
                return new AIModelCapabilities { Embeddings = true };
            }

            HashSet<string> capSet = new(caps, StringComparer.OrdinalIgnoreCase);

            return new AIModelCapabilities
            {
                Chat = capSet.Contains("completion"),
                Embeddings = capSet.Contains("embedding"),
                Vision = capSet.Contains("vision"),
                ToolUse = capSet.Contains("tools"),
            };
        }
        catch
        {
            // Fallback if /api/show fails (older Ollama versions)
            return new AIModelCapabilities { Embeddings = true };
        }
    }
}
