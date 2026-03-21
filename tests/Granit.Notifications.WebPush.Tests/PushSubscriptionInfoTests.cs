using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class PushSubscriptionInfoTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        PushSubscriptionInfo sub = new()
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
        PushSubscriptionInfo sub = new()
        {
            Endpoint = "https://push.example.com/sub1",
            P256dh = "p256dh-key",
            Auth = "auth-key",
        };

        sub.ExpirationTime.ShouldBeNull();
    }

    [Fact]
    public void IsSealed() =>
        typeof(PushSubscriptionInfo).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(PushSubscriptionInfo).GetMethod("<Clone>$").ShouldNotBeNull();

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        PushSubscriptionInfo a = new() { Endpoint = "https://push.example.com/1", P256dh = "k", Auth = "a" };
        PushSubscriptionInfo b = new() { Endpoint = "https://push.example.com/1", P256dh = "k", Auth = "a" };

        a.ShouldBe(b);
    }
}
