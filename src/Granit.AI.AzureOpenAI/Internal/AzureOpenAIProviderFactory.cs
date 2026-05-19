using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Identity;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Internal;

/// <summary>
/// Azure OpenAI implementation of <see cref="IAIProviderFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// Creates <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{String, Embedding}"/>
/// instances backed by <see cref="AzureOpenAIClient"/>. Chat clients are wrapped by
/// <see cref="TracingAzureOpenAIChatClient"/> so each call emits an OpenTelemetry GenAI span.
/// </para>
/// <para>
/// Supports API key authentication (dev/staging) and <see cref="DefaultAzureCredential"/> /
/// Managed Identity (production). The underlying <see cref="AzureOpenAIClient"/> is rebuilt
/// whenever <see cref="IOptionsMonitor{TOptions}"/> publishes a configuration change — so
/// API-key rotation from <c>Granit.Vault</c> takes effect without a process restart.
/// </para>
/// <para>
/// The transport <see cref="HttpClient"/> is sourced from <see cref="IHttpClientFactory"/>,
/// so connection pooling and DNS refresh are owned by the host.
/// </para>
/// </remarks>
internal sealed class AzureOpenAIProviderFactory : IAIProviderFactory, IAIModelCatalog, IDisposable
{
    /// <summary>Named <see cref="HttpClient"/> consumed by this factory.</summary>
    internal const string HttpClientName = "Granit.AI.AzureOpenAI";

    private readonly IOptionsMonitor<AzureOpenAIProviderOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDisposable? _changeSubscription;
    private AzureOpenAIClient _client;

    public AzureOpenAIProviderFactory(
        IOptionsMonitor<AzureOpenAIProviderOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory)
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _client = BuildClient(optionsMonitor.CurrentValue);
        _changeSubscription = optionsMonitor.OnChange(OnOptionsChanged);
    }

    /// <inheritdoc/>
    public string ProviderName => "AzureOpenAI";

    /// <inheritdoc/>
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AzureOpenAIProviderOptions options = _optionsMonitor.CurrentValue;
        string deployment = string.IsNullOrWhiteSpace(workspace.Model) ? options.DefaultDeployment : workspace.Model;
        EnforceAllowlist(options, deployment);

        AzureOpenAIClient client = Volatile.Read(ref _client);
        IChatClient inner = client.GetChatClient(deployment).AsIChatClient();
        return new TracingAzureOpenAIChatClient(inner, deployment);
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AzureOpenAIProviderOptions options = _optionsMonitor.CurrentValue;
        string deployment = options.DefaultEmbeddingDeployment;
        EnforceAllowlist(options, deployment);

        AzureOpenAIClient client = Volatile.Read(ref _client);
        return client.GetEmbeddingClient(deployment).AsIEmbeddingGenerator();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        AzureOpenAIProviderOptions options = _optionsMonitor.CurrentValue;

        // Azure OpenAI uses deployment names configured per-resource — expose the configured defaults.
        IReadOnlyList<AIModelInfo> models =
        [
            new(options.DefaultDeployment, options.DefaultDeployment, new AIModelCapabilities()),
            new(options.DefaultEmbeddingDeployment, options.DefaultEmbeddingDeployment, new AIModelCapabilities { Chat = false, Embeddings = true, Streaming = false }),
        ];

        return Task.FromResult(models);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _changeSubscription?.Dispose();
        // AzureOpenAIClient does not own the HttpClient (provided via Transport from IHttpClientFactory);
        // GC reclaims the wrapper.
    }

    private static void EnforceAllowlist(AzureOpenAIProviderOptions options, string deployment)
    {
        if (options.AllowedDeployments.Count > 0 &&
            !options.AllowedDeployments.Contains(deployment, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Azure OpenAI deployment '{deployment}' is not in the configured AllowedDeployments list. " +
                "Add it to AI:AzureOpenAI:AllowedDeployments or update the workspace.");
        }
    }

    private void OnOptionsChanged(AzureOpenAIProviderOptions newOptions)
    {
        AzureOpenAIClient next;
        try
        {
            next = BuildClient(newOptions);
        }
        catch
        {
            return;
        }

        Interlocked.Exchange(ref _client, next);
    }

    private AzureOpenAIClient BuildClient(AzureOpenAIProviderOptions options)
    {
        var endpoint = new Uri(options.Endpoint);
        HttpClient http = _httpClientFactory.CreateClient(HttpClientName);
        var clientOptions = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(http),
            NetworkTimeout = options.Timeout,
            RetryPolicy = new ClientRetryPolicy(options.MaxRetries),
        };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new AzureOpenAIClient(endpoint, new ApiKeyCredential(options.ApiKey), clientOptions);
        }

        return new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions);
    }
}
