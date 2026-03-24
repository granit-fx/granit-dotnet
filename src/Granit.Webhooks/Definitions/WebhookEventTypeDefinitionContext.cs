using System.Collections.ObjectModel;
using Granit.Localization;

namespace Granit.Webhooks.Definitions;

/// <summary>
/// Internal context implementation that collects event type definitions
/// from providers and produces an immutable dictionary.
/// </summary>
internal sealed class WebhookEventTypeDefinitionContext : IWebhookEventTypeDefinitionContext
{
    private readonly Dictionary<string, WebhookEventTypeDefinition> _definitions = [];

    /// <inheritdoc/>
    public void Add<TResource>(string name, string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var definition = new WebhookEventTypeDefinition(
            name,
            DisplayName: LocalizableString.Create<TResource>($"WebhookEventType:{name}"),
            Description: LocalizableString.Create<TResource>($"WebhookEventType:{name}:Description"),
            Category: category is not null
                ? LocalizableString.Create<TResource>($"WebhookEventTypeCategory:{category}")
                : null);

        _definitions[name] = definition;
    }

    /// <inheritdoc/>
    public void Add(WebhookEventTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        _definitions[definition.Name] = definition;
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
