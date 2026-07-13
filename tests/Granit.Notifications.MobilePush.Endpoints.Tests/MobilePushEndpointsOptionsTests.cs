using Granit.Notifications.MobilePush.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Endpoints.Tests;

public sealed class MobilePushEndpointsOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        MobilePushEndpointsOptions.SectionName.ShouldBe("Notifications:MobilePush:Endpoints");

    [Fact]
    public void RoutePrefix_Default_IsPinned() =>
        new MobilePushEndpointsOptions().RoutePrefix.ShouldBe("notifications/mobile-push");
}
