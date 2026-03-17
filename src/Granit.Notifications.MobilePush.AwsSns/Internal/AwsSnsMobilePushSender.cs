using System.Diagnostics;
using System.Text.Json;
using Amazon.SimpleNotificationService.Model;
using Granit.Notifications.MobilePush.AwsSns.Diagnostics;
using Granit.Notifications.MobilePush.AwsSns.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.MobilePush.AwsSns.Internal;

/// <summary>
/// <see cref="IMobilePushSender"/> implementation using AWS SNS Platform Applications.
/// Registered as Keyed Service with key "AwsSns".
/// </summary>
/// <remarks>
/// For each device token, a platform endpoint is created (idempotent). The notification
/// is then published to each endpoint ARN. Disabled endpoints trigger token invalidation events.
/// </remarks>
internal sealed partial class AwsSnsMobilePushSender(
    IOptionsMonitor<AwsSnsMobilePushOptions> options,
    ILogger<AwsSnsMobilePushSender> logger,
    IAwsSnsMobilePushTransport transport,
    IMobilePushEventPublisher eventPublisher) : IMobilePushSender
{
    /// <inheritdoc />
    public async Task SendAsync(MobilePushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        AwsSnsMobilePushOptions opts = options.CurrentValue;

        using Activity? activity = NotificationsMobilePushAwsSnsActivitySource.Source.StartActivity(
            NotificationsMobilePushAwsSnsActivitySource.Operations.Send);
        activity?.SetTag(NotificationsMobilePushAwsSnsActivitySource.Tags.DeviceCount, message.DeviceTokens.Count);

        string jsonPayload = BuildGcmPayload(message);

        foreach (string deviceToken in message.DeviceTokens)
        {
            await SendToDeviceAsync(opts, deviceToken, jsonPayload, cancellationToken).ConfigureAwait(false);
        }

        LogPushSent(message.DeviceTokens.Count);
    }

    private async Task SendToDeviceAsync(
        AwsSnsMobilePushOptions opts,
        string deviceToken,
        string jsonPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            CreatePlatformEndpointResponse endpoint = await transport.CreatePlatformEndpointAsync(
                new CreatePlatformEndpointRequest
                {
                    PlatformApplicationArn = opts.PlatformApplicationArn,
                    Token = deviceToken,
                },
                cancellationToken).ConfigureAwait(false);

            await transport.PublishAsync(
                new PublishRequest
                {
                    TargetArn = endpoint.EndpointArn,
                    MessageStructure = "json",
                    Message = jsonPayload,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (EndpointDisabledException)
        {
            LogEndpointDisabled(deviceToken);
            await eventPublisher.PublishTokenInvalidatedAsync(
                new MobilePushTokenInvalidated { DeviceToken = deviceToken },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildGcmPayload(MobilePushMessage message)
    {
        using var stream = new MemoryStream();
        using var outerWriter = new Utf8JsonWriter(stream);

        outerWriter.WriteStartObject();

        // The "GCM" key targets FCM/GCM endpoints; "default" is fallback
        using var gcmStream = new MemoryStream();
        using (var gcmWriter = new Utf8JsonWriter(gcmStream))
        {
            gcmWriter.WriteStartObject();
            gcmWriter.WriteStartObject("notification");
            gcmWriter.WriteString("title", message.Title);
            gcmWriter.WriteString("body", message.Body);
            gcmWriter.WriteEndObject();

            if (message.Data.HasValue)
            {
                gcmWriter.WritePropertyName("data");
                message.Data.Value.WriteTo(gcmWriter);
            }

            gcmWriter.WriteEndObject();
        }

        string gcmJson = System.Text.Encoding.UTF8.GetString(gcmStream.ToArray());

        outerWriter.WriteString("default", message.Body);
        outerWriter.WriteString("GCM", gcmJson);

        outerWriter.WriteEndObject();
        outerWriter.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS push sent to {DeviceCount} device(s)")]
    private partial void LogPushSent(int deviceCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SNS endpoint disabled for token {DeviceToken}, publishing invalidation event")]
    private partial void LogEndpointDisabled(string deviceToken);
}
