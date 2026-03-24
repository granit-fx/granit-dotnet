using Granit.Localization;

namespace Granit.Webhooks.Definitions;

/// <summary>
/// Context passed to <see cref="IWebhookEventTypeDefinitionProvider.Define"/>
/// to register webhook event type definitions.
/// </summary>
public interface IWebhookEventTypeDefinitionContext
{
    /// <summary>
    /// Registers a webhook event type using convention-based localization keys derived from
    /// <typeparamref name="TResource"/>. Keys follow the pattern:
    /// <list type="bullet">
    ///   <item><c>WebhookEventType:{name}</c> → display name</item>
    ///   <item><c>WebhookEventType:{name}:Description</c> → description</item>
    ///   <item><c>WebhookEventTypeCategory:{category}</c> → category (when non-null)</item>
    /// </list>
    /// If the same name is added twice, the last registration wins.
    /// </summary>
    /// <typeparam name="TResource">Localization resource type for key resolution.</typeparam>
    /// <param name="name">Dotted identifier (e.g. <c>"document.uploaded"</c>).</param>
    /// <param name="category">Optional grouping key (e.g. <c>"Documents"</c>).</param>
    void Add<TResource>(string name, string? category = null);

    /// <summary>
    /// Registers a webhook event type with explicit <see cref="LocalizableString"/> values.
    /// If the same name is added twice, the last registration wins.
    /// </summary>
    void Add(WebhookEventTypeDefinition definition);

    /// <summary>
    /// Returns the definition associated with the name, or <c>null</c> if not yet registered.
    /// </summary>
    WebhookEventTypeDefinition? GetOrNull(string name);
}
