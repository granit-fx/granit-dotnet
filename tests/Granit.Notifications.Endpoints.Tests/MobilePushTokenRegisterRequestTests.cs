using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.MobilePush;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class MobilePushTokenRegisterRequestTests
{
    [Fact]
    public void CanCreate_WithRequiredProperties()
    {
        var request = new MobilePushTokenRegisterRequest
        {
            DeviceToken = "fcm-token-abc",
            Platform = MobilePlatform.Android,
        };

        request.DeviceToken.ShouldBe("fcm-token-abc");
        request.Platform.ShouldBe(MobilePlatform.Android);
    }

    [Fact]
    public void CanCreate_WithIosPlatform()
    {
        var request = new MobilePushTokenRegisterRequest
        {
            DeviceToken = "apns-token-xyz",
            Platform = MobilePlatform.Ios,
        };

        request.DeviceToken.ShouldBe("apns-token-xyz");
        request.Platform.ShouldBe(MobilePlatform.Ios);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new MobilePushTokenRegisterRequest { DeviceToken = "t1", Platform = MobilePlatform.Android };
        var b = new MobilePushTokenRegisterRequest { DeviceToken = "t1", Platform = MobilePlatform.Android };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentDeviceToken_AreNotEqual()
    {
        var a = new MobilePushTokenRegisterRequest { DeviceToken = "t1", Platform = MobilePlatform.Android };
        var b = new MobilePushTokenRegisterRequest { DeviceToken = "t2", Platform = MobilePlatform.Android };

        a.ShouldNotBe(b);
    }
}
