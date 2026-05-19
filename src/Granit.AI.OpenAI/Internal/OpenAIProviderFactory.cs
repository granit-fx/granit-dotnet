using System.ClientModel;
using System.ClientModel.Primitives;
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
/// <para>
/// Creates <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{String, Embedding}"/>
/// instances backed by <see cref="OpenAIClient"/>. The chat client is wrapped by
/// <see cref="TracingOpenAIChatClient"/> so each call emits an OpenTelemetry GenAI span.
/// </para>
/// <para>
/// The underlying <see cref="OpenAIClient"/> is rebuilt whenever <see cref="IOptionsMonitor{TOptions}"/>
/// publishes a configuration change, so API-key rotation from <c>Granit.Vault</c> takes effect
/// without a process restart. The transport <see cref="HttpClient"/> is sourced from
/// <see cref="IHttpClientFactory"/> so connection pooling and DNS refresh are owned by the host.
/// </para>
/// <para>
/// Model catalog is fetched dynamically from the OpenAI <c>GET /v1/models</c> endpoint and
/// enriched with known metadata (context window, capabilities) for well-known models. Results
/// are cached for 5 minutes.
/// </para>
/// </remarks>
internal sealed class OpenAIProviderFactory : IAIProviderFactory, IAIModelCatalog, IDisposable
{
    /// <summary>Named <see cref="HttpClient"/> consumed by this factory.</summary>
    internal const string HttpClientName = "Granit.AI.OpenAI";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Known metadata for well-known OpenAI models. Models not in this map
    /// get default chat capabilities with no context window info.
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

    private readonly IOptionsMonitor<OpenAIProviderOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IDisposable? _changeSubscription;
    private readonly Lock _cacheLock = new();
    private OpenAIClient _client;
    private IReadOnlyList<AIModelInfo>? _cachedModels;
    private DateTimeOffset _cacheExpiry;

    public OpenAIProviderFactory(
        IOptionsMonitor<OpenAIProviderOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider)
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _client = BuildClient(optionsMonitor.CurrentValue);
        _changeSubscription = optionsMonitor.OnChange(OnOptionsChanged);
    }

    /// <inheritdoc/>
    public string ProviderName => "OpenAI";

    /// <inheritdoc/>
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OpenAIProviderOptions options = _optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? options.DefaultModel : workspace.Model;
        EnforceAllowlist(options, model);

        OpenAIClient client = Volatile.Read(ref _client);
        IChatClient inner = client.GetChatClient(model).AsIChatClient();
        return new TracingOpenAIChatClient(inner, model);
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OpenAIProviderOptions options = _optionsMonitor.CurrentValue;
        string model = options.DefaultEmbeddingModel;
        EnforceAllowlist(options, model);

        OpenAIClient client = Volatile.Read(ref _client);
        return client.GetEmbeddingClient(model).AsIEmbeddingGenerator();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        lock (_cacheLock)
        {
            if (_cachedModels is not null && _timeProvider.GetUtcNow() < _cacheExpiry)
            {
                return _cachedModels;
            }
        }

        OpenAIClient client = Volatile.Read(ref _client);
        OpenAIModelClient modelClient = client.GetOpenAIModelClient();

        OpenAIModelCollection apiModels = await modelClient
            .GetModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AIModelInfo> models = apiModels
            .Select(m => EnrichModel(m.Id))
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_cacheLock)
        {
            _cachedModels = models;
            _cacheExpiry = _timeProvider.GetUtcNow().Add(CacheDuration);
        }

        return models;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _changeSubscription?.Dispose();
        // OpenAIClient does not own the HttpClient (provided via Transport from IHttpClientFactory);
        // GC reclaims the wrapper.
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

    private void OnOptionsChanged(OpenAIProviderOptions newOptions)
    {
        OpenAIClient next;
        try
        {
            next = BuildClient(newOptions);
        }
        catch
        {
            // Keep the current client if the new options are unusable (e.g. transient configuration glitch).
            // The validator already rejects malformed options at startup; this guards mid-flight hot reloads.
            return;
        }

        Interlocked.Exchange(ref _client, next);
        lock (_cacheLock)
        {
            // Invalidate the model catalog cache so the next caller observes the rotated endpoint/key.
            _cachedModels = null;
            _cacheExpiry = DateTimeOffset.MinValue;
        }
    }

    private OpenAIClient BuildClient(OpenAIProviderOptions options)
    {
        var credential = new ApiKeyCredential(options.ApiKey);

        // HttpClient.Timeout left at InfiniteTimeSpan (configured in the DI extension) so the
        // SDK's NetworkTimeout drives cancellation; otherwise the inner HttpClient cancellation
        // races the pipeline's retry policy.
        HttpClient http = _httpClientFactory.CreateClient(HttpClientName);
        var clientOptions = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(http),
            NetworkTimeout = options.Timeout,
            RetryPolicy = new ClientRetryPolicy(options.MaxRetries),
        };

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            clientOptions.Endpoint = new Uri(options.Endpoint);
        }

        return new OpenAIClient(credential, clientOptions);
    }
}
