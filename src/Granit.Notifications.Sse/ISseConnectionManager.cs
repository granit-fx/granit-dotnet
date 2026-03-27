namespace Granit.Notifications.Sse;

/// <summary>
/// Thread-safe connection manager that routes SSE messages to active HTTP connections per user.
/// </summary>
public interface ISseConnectionManager
{
    /// <summary>
    /// Registers a new SSE connection for the specified user.
    /// Returns <c>null</c> when the per-user connection limit is reached.
    /// </summary>
    SseConnection? Connect(string userId);

    /// <summary>
    /// Removes an SSE connection and completes its channel.
    /// </summary>
    void Disconnect(SseConnection connection);

    /// <summary>
    /// Sends a message to all active connections for the specified user.
    /// If no connections exist, the message is silently dropped (InApp channel persists it).
    /// </summary>
    ValueTask SendToUserAsync(string userId, SseNotificationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of active SSE connections for a user.
    /// </summary>
    int GetConnectionCount(string userId);
}
