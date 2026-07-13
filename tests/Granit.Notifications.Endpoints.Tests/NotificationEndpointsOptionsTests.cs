using Granit.Notifications.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationEndpointsOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        NotificationEndpointsOptions.SectionName.ShouldBe("Notifications:Endpoints");

    [Fact]
    public void RoutePrefix_Default_IsNotifications() =>
        new NotificationEndpointsOptions().RoutePrefix.ShouldBe("notifications");
}
