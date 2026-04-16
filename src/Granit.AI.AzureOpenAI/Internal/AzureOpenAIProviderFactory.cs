using System.ClientModel;
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
/// Creates <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{String, Embedding}"/>
/// instances backed by <see cref="AzureOpenAIClient"/>. Supports API key authentication
/// (dev/staging) and <see cref="DefaultAzureCredential"/> / Managed Identity (production).
/// </remarks>
internal sealed class AzureOpenAIProviderFactory(IOptions<AzureOpenAIProviderOptions> options) : IAIProviderFactory, IAIModelCatalog
{
    private readonly AzureOpenAIProviderOptions _options = options.Value;

    /// <inheritdoc/>
    public string ProviderName => "AzureOpenAI";

    /// <inheritdoc/>
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        AzureOpenAIClient client = CreateClient();
        string deployment = workspace.Model ?? _options.DefaultDeployment;

        return client.GetChatClient(deployment).AsIChatClient();
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AIWorkspace workspace)
    {
        AzureOpenAIClient client = CreateClient();
        string deployment = _options.DefaultEmbeddingDeployment;

        return client.GetEmbeddingClient(deployment).AsIEmbeddingGenerator();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        // Azure OpenAI uses deployment names configured per-resource — expose the configured defaults.
        IReadOnlyList<AIModelInfo> models =
        [
            new(_options.DefaultDeployment, _options.DefaultDeployment, new AIModelCapabilities()),
            new(_options.DefaultEmbeddingDeployment, _options.DefaultEmbeddingDeployment, new AIModelCapabilities { Chat = false, Embeddings = true, Streaming = false }),
        ];

        return Task.FromResult(models);
    }

    private AzureOpenAIClient CreateClient()
    {
        Uri endpoint = new(_options.Endpoint);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new AzureOpenAIClient(endpoint, new ApiKeyCredential(_options.ApiKey));
        }

        return new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
    }
}
