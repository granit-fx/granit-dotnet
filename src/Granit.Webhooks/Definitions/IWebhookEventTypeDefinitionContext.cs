namespace Granit.Webhooks.Definitions;

/// <summary>
/// Context passed to <see cref="IWebhookEventTypeDefinitionProvider.Define"/>
/// to register webhook event type definitions.
/// </summary>
public interface IWebhookEventTypeDefinitionContext
{
    /// <summary>
    /// Registers a webhook event type. If the same name is added twice, the last registration wins.
    /// </summary>
    /// <param name="name">Dotted identifier (e.g. <c>"document.uploaded"</c>).</param>
    /// <param name="displayName">Human-readable label for admin UI.</param>
    /// <param name="description">Explanation of when this event fires.</param>
    /// <param name="category">Optional grouping key (e.g. <c>"Documents"</c>).</param>
    void Add(string name, string? displayName = null, string? description = null, string? category = null);

    /// <summary>
    /// Returns the definition associated with the name, or <c>null</c> if not yet registered.
    /// </summary>
    WebhookEventTypeDefinition? GetOrNull(string name);
}
