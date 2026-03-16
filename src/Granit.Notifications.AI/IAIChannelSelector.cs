namespace Granit.Notifications.AI;

/// <summary>
/// Recommends optimal delivery channels for a notification based on context analysis.
/// </summary>
/// <remarks>
/// Uses an LLM to evaluate notification severity, time of day, and available channels
/// to recommend the most appropriate subset for delivery. Returns the full available
/// channel list as a fallback when the LLM is unavailable.
/// </remarks>
public interface IAIChannelSelector
{
    /// <summary>
    /// Selects optimal delivery channels from the available set based on context analysis.
    /// </summary>
    /// <param name="context">The notification delivery context containing severity, timing, and metadata.</param>
    /// <param name="availableChannels">The list of channels that are registered and available for delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Ordered list of recommended channel names (subset of <paramref name="availableChannels"/>).
    /// Returns all available channels when the LLM is unavailable.
    /// </returns>
    Task<IReadOnlyList<string>> SelectChannelsAsync(
        NotificationDeliveryContext context,
        IReadOnlyList<string> availableChannels,
        CancellationToken cancellationToken = default);
}
