namespace Granit.Notifications.Sse.Options;

/// <summary>
/// Configuration options for the SSE notification channel.
/// </summary>
public sealed class SseChannelOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:Sse";

    /// <summary>
    /// Heartbeat interval in seconds. Keeps the connection alive through proxies and load-balancers.
    /// Default is 30 seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent SSE connections per user. Prevents resource exhaustion.
    /// Default is 10 (covers multiple browser tabs/devices).
    /// </summary>
    public int MaxConnectionsPerUser { get; set; } = 10;

    /// <summary>
    /// Maximum number of messages buffered per connection. When full, oldest messages are dropped.
    /// Default is 100.
    /// </summary>
    public int MaxBufferSize { get; set; } = 100;
}
