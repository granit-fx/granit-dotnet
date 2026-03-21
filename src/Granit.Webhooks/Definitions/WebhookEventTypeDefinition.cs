namespace Granit.Webhooks.Definitions;

/// <summary>
/// Describes a webhook event type that consumers can subscribe to.
/// </summary>
/// <param name="Name">Dotted identifier (e.g. <c>"document.uploaded"</c>).</param>
/// <param name="DisplayName">Human-readable label for admin UI.</param>
/// <param name="Description">Explanation of when this event fires.</param>
/// <param name="Category">Optional grouping key (e.g. <c>"Documents"</c>, <c>"Patients"</c>).</param>
public sealed record WebhookEventTypeDefinition(
    string Name,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null);
