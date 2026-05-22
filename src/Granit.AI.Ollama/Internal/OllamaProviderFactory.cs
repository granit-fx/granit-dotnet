using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;

namespace Granit.AI.Ollama.Internal;

/// <summary>
/// Ollama implementation of <see cref="IAIProviderFactory"/> and <see cref="IAIModelCatalog"/>.
/// </summary>
/// <remarks>
/// <para>
/// Endpoint cascade resolved by <see cref="OllamaCredentialResolver"/>. SDK clients are reused
/// per (endpoint, model) via the Singleton <see cref="OllamaClientCache"/>.
/// </para>
/// <para>
/// Model catalog (<c>GET /api/tags</c>) is queried with the Host-options endpoint and cached
/// for 30 seconds.
/// </para>
/// </remarks>
internal sealed class OllamaProviderFactory(
    IOptionsMonitor<OllamaProviderOptions> optionsMonitor,
    OllamaCredentialResolver credentialResolver,
    OllamaClientCache clientCache,
    TimeProvider timeProvider) : IAIProviderFactory, IAIModelCatalog
{
    /// <summary>
    /// Named <see cref="HttpClient"/> for Workspace/Tenant/Global-scoped endpoints —
    /// strict <see cref="AIEndpointPolicy.OllamaTenant"/> connect-time policy.
    /// </summary>
    internal const string TenantHttpClientName = "Granit.AI.Ollama";

    /// <summary>
    /// Named <see cref="HttpClient"/> for Host-scoped endpoints (operator-trusted, e.g. a
    /// private deployment) — permissive <see cref="AIEndpointPolicy.HostPermissive"/>
    /// connect-time policy.
    /// </summary>
    internal const string HostHttpClientName = "Granit.AI.Ollama.Host";

    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromSeconds(30);

    private readonly Lock _catalogLock = new();
    private IReadOnlyList<AIModelInfo>? _cachedModels;
    private DateTimeOffset _cacheExpiry;

    /// <inheritdoc/>
    public string ProviderName => "Ollama";

    /// <inheritdoc/>
    public async ValueTask<IChatClient> CreateChatClientAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OllamaProviderOptions opts = optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? opts.DefaultModel : workspace.Model;
        EnforceAllowlist(opts, model);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        OllamaApiClient inner = clientCache.GetOrCreate(credential.Endpoint!, model, credential.Scope);
        return new TracingOllamaChatClient(inner, model, credential);
    }

    /// <inheritdoc/>
    public async ValueTask<IEmbeddingGenerator<string, Embedding<float>>?> CreateEmbeddingGeneratorAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OllamaProviderOptions opts = optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? opts.DefaultModel : workspace.Model;
        EnforceAllowlist(opts, model);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        return clientCache.GetOrCreate(credential.Endpoint!, model, credential.Scope);
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

        OllamaProviderOptions opts = optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.Endpoint))
        {
            return [];
        }

        // Catalog uses the Host-options endpoint by construction → permissive connect policy
        // so an operator-trusted private/internal deployment is reachable.
        OllamaApiClient client = clientCache.GetOrCreate(
            opts.Endpoint, opts.DefaultModel, AIProviderCredentialScope.Host);

        IEnumerable<Model> localModels = await client
            .ListLocalModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        AIModelInfo[] models = await Task.WhenAll(localModels.Select(async model =>
        {
            AIModelCapabilities capabilities = await ResolveCapabilitiesAsync(client, model.Name, cancellationToken)
                .ConfigureAwait(false);
            return new AIModelInfo(model.Name, model.Name, capabilities);
        })).ConfigureAwait(false);

        lock (_catalogLock)
        {
            _cachedModels = models;
            _cacheExpiry = timeProvider.GetUtcNow().Add(CatalogCacheDuration);
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
            return new AIModelCapabilities { Embeddings = true };
        }
    }
}
