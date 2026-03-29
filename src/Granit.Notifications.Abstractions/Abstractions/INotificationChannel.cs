namespace Granit.Notifications.Abstractions;

/// <summary>
/// Pluggable notification delivery channel (InApp, Email, SMS, WhatsApp, Push, SignalR).
/// </summary>
public interface INotificationChannel
{
    /// <summary>Channel name (e.g. <see cref="NotificationChannels.InApp"/>).</summary>
    string Name { get; }

    /// <summary>
    /// Delivers a notification to a single recipient via this channel.
    /// </summary>
    Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default);
}
