using Granit.Notifications.AwsSns.Sms.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.Sms.Tests;

public sealed class NotificationsAwsSnsSmsActivitySourceTests
{
    [Fact]
    public void Name_IsExpected() =>
        NotificationsAwsSnsSmsActivitySource.Name.ShouldBe("Granit.Notifications.AwsSns.Sms");

    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsAwsSnsSmsActivitySource.Source.Name.ShouldBe("Granit.Notifications.AwsSns.Sms");

    [Fact]
    public void Operations_SendSms_HasExpectedValue() =>
        NotificationsAwsSnsSmsActivitySource.Operations.SendSms.ShouldBe("sns-sms.send");

    [Fact]
    public void Tags_Recipient_HasExpectedValue() =>
        NotificationsAwsSnsSmsActivitySource.Tags.Recipient.ShouldBe("sns-sms.recipient");
}
