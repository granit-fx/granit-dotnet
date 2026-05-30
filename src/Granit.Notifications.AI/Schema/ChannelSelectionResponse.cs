namespace Granit.Notifications.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI channel selector. The recommended channels
/// are wrapped in this fixed object rather than returned as a root-level array, because
/// provider-enforced strict schema (ADR-064) rejects a top-level array. The service intersects
/// <see cref="Channels"/> with the registered available set, so a model that invents a channel
/// contributes nothing.
/// </summary>
public sealed class ChannelSelectionResponse
{
    /// <summary>
    /// Recommended channel names, ordered by priority (most important first). Validated against
    /// the available channel list by the selector.
    /// </summary>
    public List<string> Channels { get; set; } = [];
}
