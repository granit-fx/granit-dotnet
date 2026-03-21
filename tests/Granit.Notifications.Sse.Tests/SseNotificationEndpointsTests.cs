using Granit.Notifications.Sse.Endpoints;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseNotificationEndpointsTests
{
    [Fact]
    public void HeartbeatEventType_HasExpectedValue() =>
        SseNotificationEndpoints.HeartbeatEventType.ShouldBe("__heartbeat__");
}
