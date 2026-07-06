using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushNotificationPayloadTests
{
    [Fact]
    public void IsSealed() =>
        typeof(WebPushNotificationPayload).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(WebPushNotificationPayload).GetMethod("<Clone>$").ShouldNotBeNull();
}
