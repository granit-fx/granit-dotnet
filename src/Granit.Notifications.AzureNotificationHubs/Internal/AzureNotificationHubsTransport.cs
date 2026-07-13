using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Granit.Diagnostics;
using Granit.Notifications.AzureNotificationHubs.Options;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureNotificationHubs.Internal;

/// <summary>
/// Production wrapper around <see cref="NotificationHubClient"/>.
/// Sends FCM v1 JSON payloads via <see cref="NotificationHubClient.SendDirectNotificationAsync(Notification, string, CancellationToken)"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed partial class AzureNotificationHubsTransport(
    IOptions<AzureNotificationHubsOptions> options,
    ILogger<AzureNotificationHubsTransport> logger) : IAzureNotificationHubsTransport
{
    private NotificationHubClient? _client;

    /// <inheritdoc />
    public async Task SendAsync(NotificationHubsMessage message, CancellationToken cancellationToken = default)
    {
        NotificationHubClient client = GetOrCreateClient();

        string payload = BuildFcmV1Payload(message.Title, message.Body, message.Data);

        foreach (string deviceToken in message.DeviceTokens)
        {
            try
            {
                FcmV1Notification notification = new(payload);
                await client.SendDirectNotificationAsync(notification, deviceToken, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log but do not throw — partial delivery is acceptable for push.
                // Individual token failures (expired, unregistered) must not block other recipients.
                LogTokenSendFailed(LogRedaction.Token(deviceToken), ex);
            }
        }
    }

    private NotificationHubClient GetOrCreateClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        AzureNotificationHubsOptions opts = options.Value;
        _client = new NotificationHubClient(opts.ConnectionString, opts.HubName);
        return _client;
    }

    private static string BuildFcmV1Payload(string title, string body, JsonElement? data)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("message");

            // notification block
            writer.WriteStartObject("notification");
            writer.WriteString("title", title);
            writer.WriteString("body", body);
            writer.WriteEndObject();

            // data block (optional)
            if (data is { ValueKind: JsonValueKind.Object } dataElement)
            {
                writer.WritePropertyName("data");
                dataElement.WriteTo(writer);
            }

            writer.WriteEndObject(); // message
            writer.WriteEndObject(); // root
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send push notification to device token {RedactedToken}")]
    private partial void LogTokenSendFailed(string redactedToken, Exception exception);
}
