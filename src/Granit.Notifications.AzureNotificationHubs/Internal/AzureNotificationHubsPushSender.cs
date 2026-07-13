using System.Diagnostics;
using Granit.Notifications.AzureNotificationHubs.Diagnostics;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.AzureNotificationHubs.Internal;

/// <summary>
/// <see cref="IMobilePushSender"/> implementation using Azure Notification Hubs.
/// Registered as Keyed Service with key "AzureNotificationHubs".
/// </summary>
internal sealed partial class AzureNotificationHubsPushSender(
    IAzureNotificationHubsTransport transport,
    ILogger<AzureNotificationHubsPushSender> logger) : IMobilePushSender
{
    /// <inheritdoc />
    public async Task SendAsync(MobilePushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using Activity? activity = NotificationsAzureNotificationHubsActivitySource.Source.StartActivity(
            NotificationsAzureNotificationHubsActivitySource.Operations.Send);
        activity?.SetTag(NotificationsAzureNotificationHubsActivitySource.Tags.DeviceCount, message.DeviceTokens.Count);

        NotificationHubsMessage transportMessage = new(
            message.Title,
            message.Body,
            message.DeviceTokens,
            message.Data);

        await transport.SendAsync(transportMessage, cancellationToken).ConfigureAwait(false);

        LogPushSent(message.DeviceTokens.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Azure Notification Hubs push sent to {DeviceCount} device(s)")]
    private partial void LogPushSent(int deviceCount);
}
