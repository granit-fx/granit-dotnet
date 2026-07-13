using Granit.Notifications.AwsSns.MobilePush.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.MobilePush.Tests;

public sealed class NotificationsAwsSnsMobilePushActivitySourceTests
{
    [Fact]
    public void Name_IsExpected() =>
        NotificationsAwsSnsMobilePushActivitySource.Name.ShouldBe("Granit.Notifications.AwsSns.MobilePush");

    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsAwsSnsMobilePushActivitySource.Source.Name.ShouldBe("Granit.Notifications.AwsSns.MobilePush");

    [Fact]
    public void Operations_Send_HasExpectedValue() =>
        NotificationsAwsSnsMobilePushActivitySource.Operations.Send.ShouldBe("sns-push.send");

    [Fact]
    public void Tags_DeviceCount_HasExpectedValue() =>
        NotificationsAwsSnsMobilePushActivitySource.Tags.DeviceCount.ShouldBe("sns-push.device_count");
}
