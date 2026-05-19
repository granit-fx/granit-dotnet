using Anthropic;
using Granit.AI.Anthropic.Options;
using Granit.AI.Tenancy;
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
/// <see cref="TracingAnthropicChatClient"/> so each call emits an OpenTelemetry GenAI span
/// carrying the resolved credential's <c>Scope</c> and <c>BilledToTenantId</c>.
/// </para>
/// <para>
/// Per request, the factory asks <see cref="AnthropicCredentialResolver"/> for a credential
/// (cascade Workspace &#8594; Tenant Setting &#8594; Global Setting &#8594; Host Options),
/// then looks up the matching <see cref="AnthropicClient"/> in the Singleton
/// <see cref="AnthropicClientCache"/>. Same key &#8594; same client (connection pool shared).
/// </para>
/// <para>
/// Embedding generation is not supported by Anthropic and always returns <c>null</c>.
/// </para>
/// </remarks>
internal sealed class AnthropicProviderFactory(
    IOptionsMonitor<AnthropicProviderOptions> optionsMonitor,
    AnthropicCredentialResolver credentialResolver,
    AnthropicClientCache clientCache) : IAIProviderFactory
{
    /// <summary>Named <see cref="HttpClient"/> consumed by the SDK client cache.</summary>
    internal const string HttpClientName = "Granit.AI.Anthropic";

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public async ValueTask<IChatClient> CreateChatClientAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AnthropicProviderOptions options = optionsMonitor.CurrentValue;
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? options.DefaultModel : workspace.Model;
        EnforceAllowlist(options, model);

        AIProviderCredential credential = await credentialResolver
            .ResolveAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            throw new AIProviderCredentialNotConfiguredException(
                ProviderName, workspace.Name, workspace.TenantId);
        }

        AnthropicClient sdkClient = clientCache.GetOrCreate(credential.ApiKey);
        IChatClient inner = sdkClient.AsIChatClient(model);
        return new TracingAnthropicChatClient(inner, model, credential);
    }

    /// <inheritdoc />
    /// <returns>Always <c>null</c>. Anthropic does not support embedding generation.</returns>
    public ValueTask<IEmbeddingGenerator<string, Embedding<float>>?> CreateEmbeddingGeneratorAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return ValueTask.FromResult<IEmbeddingGenerator<string, Embedding<float>>?>(null);
    }

    private static void EnforceAllowlist(AnthropicProviderOptions options, string model)
    {
        if (options.AllowedModels.Count > 0 &&
            !options.AllowedModels.Contains(model, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Anthropic model '{model}' is not in the configured AllowedModels list. " +
                "Add it to AI:Anthropic:AllowedModels or update the workspace.");
        }
    }
}
