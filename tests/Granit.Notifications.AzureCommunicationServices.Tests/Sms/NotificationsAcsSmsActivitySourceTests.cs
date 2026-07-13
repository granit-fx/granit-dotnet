using Granit.Notifications.AzureCommunicationServices.Sms.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Tests;

public sealed class NotificationsAcsSmsActivitySourceTests
{
    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsAcsSmsActivitySource.Source.Name
            .ShouldBe("Granit.Notifications.AzureCommunicationServices.Sms");

    [Fact]
    public void Operations_SendSms_HasCorrectValue() =>
        NotificationsAcsSmsActivitySource.Operations.SendSms
            .ShouldBe("acs-sms.send");
}
