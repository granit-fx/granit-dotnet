using System.Threading.Channels;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseConnectionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var connectionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<SseNotificationMessage>();

        SseConnection connection = new(connectionId, "user-1", channel);

        connection.ConnectionId.ShouldBe(connectionId);
        connection.UserId.ShouldBe("user-1");
        connection.Channel.ShouldBe(channel);
    }

    [Fact]
    public void Reader_ReturnsSameAsChannelReader()
    {
        var channel = Channel.CreateUnbounded<SseNotificationMessage>();
        SseConnection connection = new(Guid.NewGuid(), "user-1", channel);

        connection.Reader.ShouldBe(channel.Reader);
    }

    [Fact]
    public void IsSealed() =>
        typeof(SseConnection).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(SseConnection).GetMethod("<Clone>$").ShouldNotBeNull();
}
