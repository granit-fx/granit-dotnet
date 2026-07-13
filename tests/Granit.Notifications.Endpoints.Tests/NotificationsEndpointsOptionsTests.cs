using Granit.Notifications.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationsEndpointsOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        NotificationsEndpointsOptions.SectionName.ShouldBe("Notifications:Endpoints");

    [Fact]
    public void RoutePrefix_Default_IsNotifications() =>
        new NotificationsEndpointsOptions().RoutePrefix.ShouldBe("notifications");
}
