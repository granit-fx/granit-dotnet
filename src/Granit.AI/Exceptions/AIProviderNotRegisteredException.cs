namespace Granit.AI.Exceptions;

/// <summary>
/// Thrown when no <see cref="IAIProviderFactory"/> is registered for the requested provider name.
/// </summary>
public sealed class AIProviderNotRegisteredException(string providerName)
    : InvalidOperationException(
        $"No AI provider factory is registered for '{providerName}'. " +
        "Install the corresponding package (e.g. Granit.AI.OpenAI) and call its registration extension method.")
{
    /// <summary>
    /// Provider name that has no registered factory.
    /// </summary>
    public string ProviderName { get; } = providerName;
}
