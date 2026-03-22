using System.Collections.Concurrent;
using System.Threading.Channels;
using Granit.Guids;

namespace Granit.Notifications.Sse.Internal;

/// <summary>
/// Thread-safe connection manager backed by <see cref="Channel{T}"/> per connection
/// and <see cref="ConcurrentDictionary{TKey,TValue}"/> for user-to-connections mapping.
/// </summary>
internal sealed class SseConnectionManager(IGuidGenerator guidGenerator) : ISseConnectionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, SseConnection>> _connections = new();

    /// <inheritdoc/>
    public SseConnection Connect(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var channel = Channel.CreateUnbounded<SseNotificationMessage>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        SseConnection connection = new(guidGenerator.Create(), userId, channel);

        ConcurrentDictionary<Guid, SseConnection> userConnections =
            _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, SseConnection>());

        userConnections.TryAdd(connection.ConnectionId, connection);

        return connection;
    }

    /// <inheritdoc/>
    public void Disconnect(SseConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_connections.TryGetValue(connection.UserId, out ConcurrentDictionary<Guid, SseConnection>? userConnections))
        {
            userConnections.TryRemove(connection.ConnectionId, out _);
            connection.Channel.Writer.TryComplete();

            if (userConnections.IsEmpty)
            {
                _connections.TryRemove(connection.UserId, out _);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask SendToUserAsync(
        string userId,
        SseNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(userId, out ConcurrentDictionary<Guid, SseConnection>? userConnections))
        {
            return;
        }

        foreach (SseConnection connection in userConnections.Values)
        {
            // Best-effort: if channel is completed (client disconnected), skip silently
            connection.Channel.Writer.TryWrite(message);
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public int GetConnectionCount(string userId) =>
        _connections.TryGetValue(userId, out ConcurrentDictionary<Guid, SseConnection>? userConnections)
            ? userConnections.Count
            : 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (ConcurrentDictionary<Guid, SseConnection> userConnections in _connections.Values)
        {
            foreach (SseConnection connection in userConnections.Values)
            {
                connection.Channel.Writer.TryComplete();
            }
        }

        _connections.Clear();
    }
}
