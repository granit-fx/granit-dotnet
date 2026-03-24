using Granit.Localization;

namespace Granit.Webhooks.Definitions;

/// <summary>
/// Describes a webhook event type that consumers can subscribe to.
/// </summary>
/// <param name="Name">Dotted identifier (e.g. <c>"document.uploaded"</c>).</param>
/// <param name="DisplayName">Localizable label for admin UI.</param>
/// <param name="Description">Localizable explanation of when this event fires.</param>
/// <param name="Category">Localizable grouping key (e.g. <c>"Documents"</c>).</param>
public sealed record WebhookEventTypeDefinition(
    string Name,
    LocalizableString? DisplayName = null,
    LocalizableString? Description = null,
    LocalizableString? Category = null);
