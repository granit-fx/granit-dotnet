namespace Granit.Notifications.Sse.StackExchangeRedis.Options;

/// <summary>Configuration for the SSE Redis backplane.</summary>
public sealed class SseRedisBackplaneOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:Sse:StackExchangeRedis";

    /// <summary>Redis pub/sub channel carrying the SSE envelopes.</summary>
    public string ChannelName { get; set; } = "granit-notifications-sse";
}
