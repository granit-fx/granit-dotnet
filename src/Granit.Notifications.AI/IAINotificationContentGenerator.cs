namespace Granit.Notifications.AI;

/// <summary>
/// Generates notification content (subject and body) from context data using an LLM.
/// </summary>
/// <remarks>
/// Returns <c>null</c> when the LLM is unavailable or the response cannot be parsed,
/// allowing callers to fall back to template-based content generation.
/// </remarks>
public interface IAINotificationContentGenerator
{
    /// <summary>
    /// Generates a localized notification subject and body from the delivery context.
    /// </summary>
    /// <param name="context">The notification delivery context containing type, severity, data, and culture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Generated content, or <c>null</c> if the LLM is unavailable or the response is unparseable.
    /// </returns>
    Task<NotificationContent?> GenerateAsync(
        NotificationDeliveryContext context,
        CancellationToken cancellationToken = default);
}
