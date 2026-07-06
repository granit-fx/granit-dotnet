using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushSubscriptionInfoTests
{
    [Fact]
    public void ExpirationTime_DefaultsToNull()
    {
        WebPushSubscriptionInfo sub = new()
        {
            Endpoint = "https://push.example.com/sub1",
            P256dh = "p256dh-key",
            Auth = "auth-key",
        };

        sub.ExpirationTime.ShouldBeNull();
    }

    [Fact]
    public void IsSealed() =>
        typeof(WebPushSubscriptionInfo).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(WebPushSubscriptionInfo).GetMethod("<Clone>$").ShouldNotBeNull();
}
