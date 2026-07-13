// =============================================================================
// Tests - NotificationsAzureNotificationHubsActivitySource
// =============================================================================
// Verifies the activity source constants and singleton instance.
// =============================================================================

using Granit.Notifications.AzureNotificationHubs.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureNotificationHubs.Tests;

public sealed class NotificationsAzureNotificationHubsActivitySourceTests
{
    [Fact]
    public void Name_IsFullyQualifiedPackageName() =>
        NotificationsAzureNotificationHubsActivitySource.Name
            .ShouldBe("Granit.Notifications.AzureNotificationHubs");

    [Fact]
    public void Source_IsNotNull() =>
        NotificationsAzureNotificationHubsActivitySource.Source.ShouldNotBeNull();

    [Fact]
    public void Source_Name_MatchesConstant() =>
        NotificationsAzureNotificationHubsActivitySource.Source.Name
            .ShouldBe(NotificationsAzureNotificationHubsActivitySource.Name);

    [Fact]
    public void Operations_Send_IsAnhDotSend() =>
        NotificationsAzureNotificationHubsActivitySource.Operations.Send.ShouldBe("anh.send");

    [Fact]
    public void Tags_DeviceCount_IsAnhDotDeviceCount() =>
        NotificationsAzureNotificationHubsActivitySource.Tags.DeviceCount.ShouldBe("anh.device_count");
}
