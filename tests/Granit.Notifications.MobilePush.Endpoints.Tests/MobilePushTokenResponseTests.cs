using Granit.Notifications.MobilePush.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Endpoints.Tests;

public sealed class MobilePushTokenResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var response = new MobilePushTokenResponse("token-abc", MobilePlatform.Ios, createdAt);

        response.DeviceToken.ShouldBe("token-abc");
        response.Platform.ShouldBe(MobilePlatform.Ios);
        response.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var a = new MobilePushTokenResponse("t1", MobilePlatform.Android, createdAt);
        var b = new MobilePushTokenResponse("t1", MobilePlatform.Android, createdAt);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var a = new MobilePushTokenResponse("t1", MobilePlatform.Android, createdAt);
        var b = new MobilePushTokenResponse("t2", MobilePlatform.Android, createdAt);

        a.ShouldNotBe(b);
    }
}
