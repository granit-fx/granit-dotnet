// =============================================================================
// Tests - SseConnectionManager
// =============================================================================
// Verifies thread-safe connection tracking, multi-tab support, message routing,
// disconnect cleanup, and graceful handling of completed channels.
// =============================================================================

using Granit.Guids;
using Granit.Notifications.Sse.Internal;
using Granit.Notifications.Sse.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseConnectionManagerTests : IDisposable
{
    private static readonly IOptions<SseChannelOptions> s_options =
        Microsoft.Extensions.Options.Options.Create(new SseChannelOptions());

    private readonly SseConnectionManager _manager = new(SimpleGuidGenerator.Instance, s_options);

    [Fact]
    public void Connect_ReturnsConnectionWithUserId()
    {
        SseConnection connection = _manager.Connect("user-1")!;

        connection.UserId.ShouldBe("user-1");
        connection.ConnectionId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Connect_CreatesDistinctConnectionIds()
    {
        SseConnection conn1 = _manager.Connect("user-1")!;
        SseConnection conn2 = _manager.Connect("user-1")!;

        conn1.ConnectionId.ShouldNotBe(conn2.ConnectionId);
    }

    [Fact]
    public void Connect_ThrowsOnNull() =>
        Should.Throw<ArgumentNullException>(() => _manager.Connect(null!));

    [Fact]
    public void GetConnectionCount_ReturnsZeroForUnknownUser() =>
        _manager.GetConnectionCount("unknown").ShouldBe(0);

    [Fact]
    public void GetConnectionCount_TracksMultipleConnections()
    {
        _manager.Connect("user-1");
        _manager.Connect("user-1");
        _manager.Connect("user-2");

        _manager.GetConnectionCount("user-1").ShouldBe(2);
        _manager.GetConnectionCount("user-2").ShouldBe(1);
    }

    [Fact]
    public void Disconnect_RemovesConnection()
    {
        SseConnection connection = _manager.Connect("user-1")!;
        _manager.Disconnect(connection);

        _manager.GetConnectionCount("user-1").ShouldBe(0);
    }

    [Fact]
    public void Disconnect_CompletesChannel()
    {
        SseConnection connection = _manager.Connect("user-1")!;
        _manager.Disconnect(connection);

        connection.Channel.Reader.Completion.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void Disconnect_OnlyRemovesTargetConnection()
    {
        SseConnection conn1 = _manager.Connect("user-1")!;
        _manager.Connect("user-1");

        _manager.Disconnect(conn1);

        _manager.GetConnectionCount("user-1").ShouldBe(1);
    }

    [Fact]
    public void Disconnect_ThrowsOnNull() =>
        Should.Throw<ArgumentNullException>(() => _manager.Disconnect(null!));

    [Fact]
    public async Task SendToUserAsync_WritesToAllConnections()
    {
        SseConnection conn1 = _manager.Connect("user-1")!;
        SseConnection conn2 = _manager.Connect("user-1")!;
        SseNotificationMessage message = BuildMessage();

        await _manager.SendToUserAsync("user-1", message, TestContext.Current.CancellationToken);

        conn1.Reader.TryRead(out SseNotificationMessage? msg1).ShouldBeTrue();
        msg1.ShouldBe(message);
        conn2.Reader.TryRead(out SseNotificationMessage? msg2).ShouldBeTrue();
        msg2.ShouldBe(message);
    }

    [Fact]
    public async Task SendToUserAsync_SilentlyDropsForUnknownUser() =>
        // Should not throw — InApp channel persists the message
        await Should.NotThrowAsync(async () => await _manager.SendToUserAsync("unknown", BuildMessage(), TestContext.Current.CancellationToken));

    [Fact]
    public async Task SendToUserAsync_SkipsCompletedChannels()
    {
        SseConnection conn1 = _manager.Connect("user-1")!;
        SseConnection conn2 = _manager.Connect("user-1")!;
        conn1.Channel.Writer.TryComplete();

        SseNotificationMessage message = BuildMessage();
        await _manager.SendToUserAsync("user-1", message, TestContext.Current.CancellationToken);

        // conn1 is completed, TryWrite returns false — no error thrown
        conn1.Reader.TryRead(out _).ShouldBeFalse();
        conn2.Reader.TryRead(out SseNotificationMessage? msg).ShouldBeTrue();
        msg.ShouldBe(message);
    }

    [Fact]
    public void Dispose_CompletesAllChannels()
    {
        SseConnection conn1 = _manager.Connect("user-1")!;
        SseConnection conn2 = _manager.Connect("user-2")!;

        _manager.Dispose();

        conn1.Channel.Reader.Completion.IsCompleted.ShouldBeTrue();
        conn2.Channel.Reader.Completion.IsCompleted.ShouldBeTrue();
    }

    public void Dispose() => _manager.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SseNotificationMessage BuildMessage() => new()
    {
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        Severity = NotificationSeverity.Info,
        Data = System.Text.Json.JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UtcNow,
    };
}
