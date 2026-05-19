using Anthropic;
using Granit.AI.Anthropic.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Internal;

/// <summary>
/// Anthropic implementation of <see cref="IAIProviderFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// Creates <see cref="IChatClient"/> instances backed by the Anthropic SDK, wrapped by
/// <see cref="TracingAnthropicChatClient"/> so each call emits an OpenTelemetry span.
/// </para>
/// <para>
/// The underlying <see cref="AnthropicClient"/> is rebuilt whenever
/// <see cref="IOptionsMonitor{TOptions}"/> publishes a configuration change, so API-key
/// rotation from <c>Granit.Vault</c> takes effect without a process restart. The
/// transport <see cref="HttpClient"/> is sourced from <see cref="IHttpClientFactory"/>
/// so connection pooling, DNS refresh, and resilience handlers are owned by the host.
/// </para>
/// <para>
/// Embedding generation is not supported by Anthropic and always returns <c>null</c>.
/// </para>
/// </remarks>
internal sealed class AnthropicProviderFactory : IAIProviderFactory, IDisposable
{
    /// <summary>Named <see cref="HttpClient"/> consumed by this factory.</summary>
    internal const string HttpClientName = "Granit.AI.Anthropic";

    private readonly IOptionsMonitor<AnthropicProviderOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDisposable? _changeSubscription;
    private AnthropicClient _client;

    public AnthropicProviderFactory(
        IOptionsMonitor<AnthropicProviderOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory)
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _client = BuildClient(optionsMonitor.CurrentValue);
        _changeSubscription = optionsMonitor.OnChange(OnOptionsChanged);
    }

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AnthropicProviderOptions options = _optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? options.DefaultModel : workspace.Model;

        if (options.AllowedModels.Count > 0 &&
            !options.AllowedModels.Contains(model, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Anthropic model '{model}' is not in the configured AllowedModels list. " +
                "Add it to AI:Anthropic:AllowedModels or update the workspace.");
        }

        AnthropicClient client = Volatile.Read(ref _client);
        IChatClient inner = client.AsIChatClient(model);
        return new TracingAnthropicChatClient(inner, model);
    }

    /// <inheritdoc />
    /// <returns>Always <c>null</c>. Anthropic does not support embedding generation.</returns>
    public IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _changeSubscription?.Dispose();
        Volatile.Read(ref _client).Dispose();
    }

    private void OnOptionsChanged(AnthropicProviderOptions newOptions)
    {
        AnthropicClient? next;
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

        AnthropicClient previous = Interlocked.Exchange(ref _client, next);
        // In-flight IChatClient wrappers still hold a reference to 'previous'; let GC reclaim it once
        // they're released. The HttpClient is owned by IHttpClientFactory and is not disposed here.
    }

    private AnthropicClient BuildClient(AnthropicProviderOptions options) => new()
    {
        ApiKey = options.ApiKey,
        HttpClient = _httpClientFactory.CreateClient(HttpClientName),
        Timeout = options.Timeout,
        MaxRetries = options.MaxRetries,
    };
}
