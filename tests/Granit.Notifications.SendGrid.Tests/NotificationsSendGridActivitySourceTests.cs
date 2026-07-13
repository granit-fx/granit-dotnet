using Granit.Notifications.SendGrid.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SendGrid.Tests;

public sealed class NotificationsSendGridActivitySourceTests
{
    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsSendGridActivitySource.Source.Name
            .ShouldBe("Granit.Notifications.SendGrid");

    [Fact]
    public void Operations_Send_HasCorrectValue() =>
        NotificationsSendGridActivitySource.Operations.Send
            .ShouldBe("sendgrid.send");
}
