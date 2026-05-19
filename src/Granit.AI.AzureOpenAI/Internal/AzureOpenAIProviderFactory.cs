using Azure.AI.OpenAI;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Internal;

/// <summary>
/// Azure OpenAI implementation of <see cref="IAIProviderFactory"/> and <see cref="IAIModelCatalog"/>.
/// </summary>
/// <remarks>
/// Cascade is resolved by <see cref="AzureOpenAICredentialResolver"/>; SDK clients are reused via
/// <see cref="AzureOpenAIClientCache"/>. Managed Identity is opt-in through
/// <see cref="AzureOpenAIProviderOptions.AllowManagedIdentityFallback"/>.
/// </remarks>
internal sealed class AzureOpenAIProviderFactory(
    IOptionsMonitor<AzureOpenAIProviderOptions> optionsMonitor,
    AzureOpenAICredentialResolver credentialResolver,
    AzureOpenAIClientCache clientCache) : IAIProviderFactory, IAIModelCatalog
{
    /// <summary>Named <see cref="HttpClient"/> consumed by the SDK client cache.</summary>
    internal const string HttpClientName = "Granit.AI.AzureOpenAI";

    /// <inheritdoc/>
    public string ProviderName => "AzureOpenAI";

    /// <inheritdoc/>
    public async ValueTask<IChatClient> CreateChatClientAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AzureOpenAIProviderOptions opts = optionsMonitor.CurrentValue;
        string deployment = string.IsNullOrWhiteSpace(workspace.Model) ? opts.DefaultDeployment : workspace.Model;
        EnforceAllowlist(opts, deployment);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        AzureOpenAIClient sdk = ResolveSdkClient(credential);
        IChatClient inner = sdk.GetChatClient(deployment).AsIChatClient();
        return new TracingAzureOpenAIChatClient(inner, deployment, credential);
    }

    /// <inheritdoc/>
    public async ValueTask<IEmbeddingGenerator<string, Embedding<float>>?> CreateEmbeddingGeneratorAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AzureOpenAIProviderOptions opts = optionsMonitor.CurrentValue;
        string deployment = opts.DefaultEmbeddingDeployment;
        EnforceAllowlist(opts, deployment);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        AzureOpenAIClient sdk = ResolveSdkClient(credential);
        return sdk.GetEmbeddingClient(deployment).AsIEmbeddingGenerator();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        AzureOpenAIProviderOptions opts = optionsMonitor.CurrentValue;

        IReadOnlyList<AIModelInfo> models =
        [
            new(opts.DefaultDeployment, opts.DefaultDeployment, new AIModelCapabilities()),
            new(opts.DefaultEmbeddingDeployment, opts.DefaultEmbeddingDeployment, new AIModelCapabilities { Chat = false, Embeddings = true, Streaming = false }),
        ];

        return Task.FromResult(models);
    }

    private AzureOpenAIClient ResolveSdkClient(AIProviderCredential credential)
    {
        if (string.IsNullOrWhiteSpace(credential.Endpoint))
        {
            throw new InvalidOperationException("Azure OpenAI credential resolution returned no endpoint.");
        }

        return credential.Scope == AIProviderCredentialScope.ManagedIdentity
            ? clientCache.GetOrCreateManagedIdentity(credential.Endpoint)
            : clientCache.GetOrCreate(credential.ApiKey!, credential.Endpoint);
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
}
