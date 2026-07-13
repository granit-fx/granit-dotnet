using Granit.Notifications.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests.Options;

public sealed class NotificationsEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        NotificationsEndpointsOptions.SectionName.ShouldBe("Notifications:Endpoints");
}
