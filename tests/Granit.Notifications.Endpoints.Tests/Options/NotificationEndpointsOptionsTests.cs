using Granit.Notifications.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests.Options;

public sealed class NotificationEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        NotificationEndpointsOptions.SectionName.ShouldBe("Notifications:Endpoints");
}
