using Granit.AI.Workspaces;
using Microsoft.Extensions.Logging;

namespace Granit.AI.Internal;

/// <summary>
/// Default resolver that looks up model capabilities from the provider's <see cref="IAIModelCatalog"/>.
/// </summary>
/// <remarks>
/// <para>
/// No additional caching is applied — each provider's catalog implementation
/// already caches internally (OpenAI: 5 min, Ollama: 30 s, Azure: static).
/// </para>
/// <para>
/// Catalog calls reach external services and can fail (provider unreachable, credentials
/// missing, transient HTTP errors). A failure here must not propagate: it would crash the
/// workspaces listing endpoint and hide every other workspace because of one bad provider.
/// Transient and configuration faults are logged and surfaced as <c>null</c> capabilities,
/// which the response model already treats as "unknown".
/// </para>
/// </remarks>
internal sealed partial class DefaultAIWorkspaceCapabilityResolver(
    IEnumerable<IAIProviderFactory> providerFactories,
    ILogger<DefaultAIWorkspaceCapabilityResolver> logger) : IAIWorkspaceCapabilityResolver
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

        IReadOnlyList<AIModelInfo> models;
        try
        {
            models = await catalog
                .GetAvailableModelsAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            LogCatalogUnavailable(providerName, modelId, ex.Message);
            return null;
        }

        return models
            .FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase))
            ?.Capabilities;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AI provider '{Provider}' catalog unavailable while resolving capabilities for model '{Model}': {Error}. Returning null (unknown) capabilities.")]
    private partial void LogCatalogUnavailable(string provider, string model, string error);
}
