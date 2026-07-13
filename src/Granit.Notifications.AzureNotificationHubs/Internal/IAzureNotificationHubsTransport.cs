using System.Text.Json;

namespace Granit.Notifications.AzureNotificationHubs.Internal;

/// <summary>
/// Thin abstraction over <c>NotificationHubClient</c> to allow
/// unit testing without a real Azure Notification Hubs endpoint.
/// </summary>
internal interface IAzureNotificationHubsTransport
{
    /// <summary>Sends a push notification via Azure Notification Hubs.</summary>
    Task SendAsync(NotificationHubsMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Internal message model for the Azure Notification Hubs transport.</summary>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification body text.</param>
/// <param name="DeviceTokens">Target device tokens.</param>
/// <param name="Data">Optional data payload.</param>
internal sealed record NotificationHubsMessage(
    string Title,
    string Body,
    IReadOnlyList<string> DeviceTokens,
    JsonElement? Data);
