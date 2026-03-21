namespace Granit.Webhooks.Definitions;

/// <summary>
/// Read-only registry of all declared webhook event types.
/// Built once at startup from all <see cref="IWebhookEventTypeDefinitionProvider"/> implementations.
/// </summary>
public interface IWebhookEventTypeRegistry
{
    /// <summary>
    /// Returns all registered event types, pre-sorted by <see cref="WebhookEventTypeDefinition.Category"/>
    /// then <see cref="WebhookEventTypeDefinition.Name"/>.
    /// </summary>
    IReadOnlyList<WebhookEventTypeDefinition> GetAll();

    /// <summary>
    /// Returns the definition associated with the event type name, or <c>null</c> if unknown.
    /// </summary>
    WebhookEventTypeDefinition? GetOrNull(string eventTypeName);

    /// <summary>
    /// Returns <c>true</c> if the event type name is registered.
    /// </summary>
    bool Exists(string eventTypeName);
}
