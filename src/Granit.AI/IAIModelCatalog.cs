namespace Granit.AI;

/// <summary>
/// Provides the list of models available from an AI provider.
/// </summary>
/// <remarks>
/// Implemented alongside <see cref="IAIProviderFactory"/> by each provider package.
/// Some providers (e.g. Ollama) discover models dynamically, so the method is asynchronous.
/// </remarks>
public interface IAIModelCatalog
{
    /// <summary>
    /// Returns the models currently available from this provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available models with their capabilities.</returns>
    Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
}
