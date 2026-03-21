using System.Collections.ObjectModel;

namespace Granit.Webhooks.Definitions;

/// <summary>
/// Singleton registry that aggregates all <see cref="IWebhookEventTypeDefinitionProvider"/>
/// implementations at startup and builds an immutable, pre-sorted snapshot.
/// </summary>
internal sealed class WebhookEventTypeRegistry : IWebhookEventTypeRegistry
{
    private readonly ReadOnlyDictionary<string, WebhookEventTypeDefinition> _definitions;
    private readonly IReadOnlyList<WebhookEventTypeDefinition> _sorted;

    public WebhookEventTypeRegistry(IEnumerable<IWebhookEventTypeDefinitionProvider> providers)
    {
        WebhookEventTypeDefinitionContext context = new();
        foreach (IWebhookEventTypeDefinitionProvider provider in providers)
        {
            provider.Define(context);
        }

        _definitions = context.Build();
        _sorted = [.. _definitions.Values
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<WebhookEventTypeDefinition> GetAll() => _sorted;

    /// <inheritdoc/>
    public WebhookEventTypeDefinition? GetOrNull(string eventTypeName) =>
        _definitions.GetValueOrDefault(eventTypeName);

    /// <inheritdoc/>
    public bool Exists(string eventTypeName) =>
        _definitions.ContainsKey(eventTypeName);
}
