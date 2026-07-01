using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushSubscriptionInfoTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        WebPushSubscriptionInfo sub = new()
        {
            Endpoint = "https://push.example.com/sub1",
            P256dh = "p256dh-key",
            Auth = "auth-key",
            ExpirationTime = 1234567890,
        };

        sub.Endpoint.ShouldBe("https://push.example.com/sub1");
        sub.P256dh.ShouldBe("p256dh-key");
        sub.Auth.ShouldBe("auth-key");
        sub.ExpirationTime.ShouldBe(1234567890);
    }

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

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        WebPushSubscriptionInfo a = new() { Endpoint = "https://push.example.com/1", P256dh = "k", Auth = "a" };
        WebPushSubscriptionInfo b = new() { Endpoint = "https://push.example.com/1", P256dh = "k", Auth = "a" };

        a.ShouldBe(b);
    }
}
