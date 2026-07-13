using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Notifications.GoogleFcm.Diagnostics;
using Granit.Notifications.GoogleFcm.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.GoogleFcm.Internal;

/// <summary>
/// <see cref="IMobilePushSender"/> implementation using Firebase Cloud Messaging HTTP v1 API.
/// Registered as Keyed Service with key "GoogleFcm".
/// </summary>
/// <remarks>
/// The push payload must NOT contain PII or health data (ISO 27001 compliance).
/// It serves as a wake-up signal — the actual content is fetched from the Granit API.
/// </remarks>
internal sealed partial class GoogleFcmMobilePushSender(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<GoogleFcmOptions> options,
    IMobilePushEventPublisher eventPublisher,
    ILogger<GoogleFcmMobilePushSender> logger) : IMobilePushSender
{
    private const string FcmHttpClientName = "GoogleFcmPush";

    /// <inheritdoc />
    public async Task SendAsync(MobilePushMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = NotificationsGoogleFcmActivitySource.Source.StartActivity(NotificationsGoogleFcmActivitySource.Operations.SendPush);
        HttpClient client = httpClientFactory.CreateClient(FcmHttpClientName);

        List<Exception>? failures = null;

        foreach (string token in message.DeviceTokens)
        {
            try
            {
                await SendToTokenAsync(client, token, message, cancellationToken).ConfigureAwait(false);
            }
            catch (FcmTokenUnregisteredException)
            {
                LogTokenUnregistered(LogRedaction.Token(token));
                await eventPublisher.PublishTokenInvalidatedAsync(new MobilePushTokenInvalidated
                {
                    DeviceToken = token,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSendFailed(LogRedaction.Token(token), ex);
                (failures ??= []).Add(ex);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException(
                $"FCM delivery failed for {failures.Count}/{message.DeviceTokens.Count} token(s)",
                failures);
        }
    }

    private async Task SendToTokenAsync(HttpClient client, string token, MobilePushMessage message, CancellationToken cancellationToken)
    {
        string projectId = options.CurrentValue.ProjectId;

        var payload = new FcmPayload
        {
            Message = new FcmPayloadMessage
            {
                Token = token,
                Notification = new FcmNotification
                {
                    Title = message.Title,
                    Body = message.Body,
                },
                Data = message.Data?.Deserialize<Dictionary<string, string>>(),
            },
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"v1/projects/{projectId}/messages:send",
            payload,
            FcmJsonContext.Default.FcmPayload,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase))
            {
                throw new FcmTokenUnregisteredException(token);
            }

            response.EnsureSuccessStatusCode();
        }

        LogMessageSent(LogRedaction.Token(token), projectId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "FCM push sent to token {RedactedToken} for project {ProjectId}")]
    private partial void LogMessageSent(string redactedToken, string projectId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FCM token {RedactedToken} is unregistered, publishing invalidation event")]
    private partial void LogTokenUnregistered(string redactedToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FCM push delivery failed for token {RedactedToken}")]
    private partial void LogSendFailed(string redactedToken, Exception exception);
}

/// <summary>Thrown when a device token is no longer registered with FCM.</summary>
internal sealed class FcmTokenUnregisteredException(string token)
    : Exception($"FCM token is unregistered: {LogRedaction.Token(token)}");

internal sealed record FcmPayload
{
    [JsonPropertyName("message")]
    public required FcmPayloadMessage Message { get; init; }
}

internal sealed record FcmPayloadMessage
{
    [JsonPropertyName("token")]
    public required string Token { get; init; }

    [JsonPropertyName("notification")]
    public required FcmNotification Notification { get; init; }

    [JsonPropertyName("data")]
    public Dictionary<string, string>? Data { get; init; }
}

internal sealed record FcmNotification
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("body")]
    public required string Body { get; init; }
}

[JsonSerializable(typeof(FcmPayload))]
internal sealed partial class FcmJsonContext : JsonSerializerContext;
