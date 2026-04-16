namespace Granit.AI.Workspaces;

/// <summary>
/// Resolves <see cref="AIModelCapabilities"/> for a given provider and model combination.
/// </summary>
/// <remarks>
/// Used by workspace endpoints to enrich responses with capability metadata.
/// The default implementation delegates to the provider's <see cref="IAIModelCatalog"/>
/// when available, leveraging the provider's internal cache.
/// </remarks>
public interface IAIWorkspaceCapabilityResolver
{
    /// <summary>
    /// Resolves capabilities for the specified provider and model.
    /// </summary>
    /// <param name="providerName">Provider identifier (e.g. <c>OpenAI</c>, <c>Ollama</c>).</param>
    /// <param name="modelId">Model identifier (e.g. <c>gpt-4o</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model's capabilities, or <c>null</c> if the provider or model is not found.</returns>
    Task<AIModelCapabilities?> ResolveAsync(
        string providerName,
        string modelId,
        CancellationToken cancellationToken = default);
}
