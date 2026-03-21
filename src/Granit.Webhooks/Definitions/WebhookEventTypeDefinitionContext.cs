using System.Collections.ObjectModel;

namespace Granit.Webhooks.Definitions;

/// <summary>
/// Internal context implementation that collects event type definitions
/// from providers and produces an immutable dictionary.
/// </summary>
internal sealed class WebhookEventTypeDefinitionContext : IWebhookEventTypeDefinitionContext
{
    private readonly Dictionary<string, WebhookEventTypeDefinition> _definitions = [];

    /// <inheritdoc/>
    public void Add(string name, string? displayName = null, string? description = null, string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _definitions[name] = new WebhookEventTypeDefinition(name, displayName, description, category);
    }

    /// <inheritdoc/>
    public WebhookEventTypeDefinition? GetOrNull(string name) =>
        _definitions.GetValueOrDefault(name);

    /// <summary>
    /// Returns an immutable snapshot of the collected definitions.
    /// </summary>
    internal ReadOnlyDictionary<string, WebhookEventTypeDefinition> Build() =>
        _definitions.AsReadOnly();
}
