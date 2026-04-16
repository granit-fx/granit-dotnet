using Granit.AI.Workspaces;

namespace Granit.AI.Internal;

/// <summary>
/// Default resolver that looks up model capabilities from the provider's <see cref="IAIModelCatalog"/>.
/// </summary>
/// <remarks>
/// No additional caching is applied — each provider's catalog implementation
/// already caches internally (OpenAI: 5 min, Ollama: 30 s, Azure: static).
/// </remarks>
internal sealed class DefaultAIWorkspaceCapabilityResolver(
    IEnumerable<IAIProviderFactory> providerFactories) : IAIWorkspaceCapabilityResolver
{
    private readonly Dictionary<string, IAIProviderFactory> _providers =
        providerFactories.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public async Task<AIModelCapabilities?> ResolveAsync(
        string providerName,
        string modelId,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(providerName, out IAIProviderFactory? factory))
        {
            return null;
        }

        if (factory is not IAIModelCatalog catalog)
        {
            return null;
        }

        IReadOnlyList<AIModelInfo> models = await catalog
            .GetAvailableModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        return models
            .FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase))
            ?.Capabilities;
    }
}
