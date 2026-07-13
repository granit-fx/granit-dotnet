using Granit.Notifications.WebPush.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Endpoints.Tests;

public sealed class WebPushEndpointsOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        WebPushEndpointsOptions.SectionName.ShouldBe("Notifications:WebPush:Endpoints");

    [Fact]
    public void RoutePrefix_Default_IsPinned() =>
        new WebPushEndpointsOptions().RoutePrefix.ShouldBe("notifications/web-push");
}
