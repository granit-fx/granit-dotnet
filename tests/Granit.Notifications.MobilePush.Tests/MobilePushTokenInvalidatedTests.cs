using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Tests;

public sealed class MobilePushTokenInvalidatedTests
{
    [Fact]
    public void Constructor_WithTenantId_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();

        var sut = new MobilePushTokenInvalidated
        {
            DeviceToken = "fcm-token-abc123",
            TenantId = tenantId
        };

        sut.DeviceToken.ShouldBe("fcm-token-abc123");
        sut.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Constructor_WithoutTenantId_TenantIdIsNull()
    {
        var sut = new MobilePushTokenInvalidated
        {
            DeviceToken = "apns-token-xyz789"
        };

        sut.DeviceToken.ShouldBe("apns-token-xyz789");
        sut.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var tenantId = Guid.NewGuid();

        var a = new MobilePushTokenInvalidated { DeviceToken = "token", TenantId = tenantId };
        var b = new MobilePushTokenInvalidated { DeviceToken = "token", TenantId = tenantId };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentDeviceToken_AreNotEqual()
    {
        var a = new MobilePushTokenInvalidated { DeviceToken = "token-a" };
        var b = new MobilePushTokenInvalidated { DeviceToken = "token-b" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_NullTenantId_BothNull_AreEqual()
    {
        var a = new MobilePushTokenInvalidated { DeviceToken = "token" };
        var b = new MobilePushTokenInvalidated { DeviceToken = "token" };

        a.ShouldBe(b);
    }
}
