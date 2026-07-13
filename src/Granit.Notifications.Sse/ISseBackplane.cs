namespace Granit.Notifications.Sse;

/// <summary>
/// Cross-replica fan-out for SSE messages. Without a backplane, the in-memory
/// <see cref="ISseConnectionManager"/> only reaches connections on the local node —
/// a notification dispatched on pod A never reaches an SSE client connected to pod B.
/// </summary>
/// <remarks>
/// Implementations publish to every node (including the local one); each node's subscriber
/// delivers to its local connections via <see cref="ISseConnectionManager.SendToUserAsync"/>.
/// Reference implementation: <c>Granit.Notifications.Sse.StackExchangeRedis</c>.
/// </remarks>
public interface ISseBackplane
{
    /// <summary>Publishes the message to all replicas for local delivery to the user's connections.</summary>
    ValueTask PublishAsync(string userId, SseNotificationMessage message, CancellationToken cancellationToken = default);
}
