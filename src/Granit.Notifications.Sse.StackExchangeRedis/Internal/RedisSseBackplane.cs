using System.Text.Json;
using Granit.Notifications.Sse.StackExchangeRedis.Diagnostics;
using Granit.Notifications.Sse.StackExchangeRedis.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.Notifications.Sse.StackExchangeRedis.Internal;

/// <summary>
/// Redis pub/sub <see cref="ISseBackplane"/>: publish goes to every replica (including the
/// local one); each node's subscription delivers to its local connections only, via
/// <see cref="ISseConnectionManager.SendToUserAsync"/>.
/// </summary>
/// <remarks>
/// Subscriber resilience: deserialization and local delivery are wrapped in a typed catch —
/// a malformed or oversized envelope is logged (scrub-free: envelopes carry no vendor
/// payloads), counted (<c>granit.notifications.sse.backplane.message_dropped</c>) and
/// <b>skipped</b>. A poison message must never kill the node's listen loop.
/// </remarks>
internal sealed partial class RedisSseBackplane(
    IConnectionMultiplexer connectionMultiplexer,
    ISseConnectionManager connectionManager,
    IOptions<SseRedisBackplaneOptions> options,
    SseRedisBackplaneMetrics metrics,
    ILogger<RedisSseBackplane> logger) : ISseBackplane, IHostedService
{
    private RedisChannel Channel => RedisChannel.Literal(options.Value.ChannelName);

    /// <summary>Wire envelope carried on the Redis channel.</summary>
    internal sealed record SseEnvelope(string UserId, SseNotificationMessage Message);

    /// <inheritdoc />
    public async ValueTask PublishAsync(
        string userId, SseNotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(message);

        string payload = JsonSerializer.Serialize(new SseEnvelope(userId, message));
        await connectionMultiplexer.GetSubscriber()
            .PublishAsync(Channel, payload)
            .ConfigureAwait(false);
        metrics.RecordPublished();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ISubscriber subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(Channel, (channel, value) => _ = DeliverLocallyAsync(value)).ConfigureAwait(false);
        LogSubscribed(options.Value.ChannelName);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken) =>
        await connectionMultiplexer.GetSubscriber().UnsubscribeAsync(Channel).ConfigureAwait(false);

    /// <summary>
    /// Handles one envelope from Redis. Never throws — resilience contract of the listen loop.
    /// </summary>
    internal async Task DeliverLocallyAsync(RedisValue value)
    {
        try
        {
            SseEnvelope? envelope = JsonSerializer.Deserialize<SseEnvelope>((string)value!);
            if (envelope is null || string.IsNullOrEmpty(envelope.UserId))
            {
                metrics.RecordDropped("empty_envelope");
                LogMessageDropped("empty_envelope");
                return;
            }

            await connectionManager
                .SendToUserAsync(envelope.UserId, envelope.Message, CancellationToken.None)
                .ConfigureAwait(false);
            metrics.RecordDelivered();
        }
        catch (Exception ex) when (ex is JsonException or InvalidCastException or ArgumentException)
        {
            metrics.RecordDropped("deserialization");
            LogMessageDropped("deserialization");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Local delivery failure (e.g. a connection tore down mid-write): count and move on.
            metrics.RecordDropped("delivery");
            LogDeliveryFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SSE Redis backplane subscribed to channel '{ChannelName}'.")]
    private partial void LogSubscribed(string channelName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSE backplane message dropped ({Reason}) — the listen loop continues.")]
    private partial void LogMessageDropped(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSE backplane local delivery failed — message dropped, the listen loop continues.")]
    private partial void LogDeliveryFailed(Exception exception);
}
