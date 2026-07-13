using Granit.Notifications.Scaleway.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Scaleway.Tests;

public sealed class NotificationsScalewayActivitySourceTests
{
    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsScalewayActivitySource.Source.Name
            .ShouldBe("Granit.Notifications.Scaleway");

    [Fact]
    public void Operations_Send_HasCorrectValue() =>
        NotificationsScalewayActivitySource.Operations.Send
            .ShouldBe("scaleway-email.send");
}
