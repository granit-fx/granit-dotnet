using Granit.Notifications.AwsSes.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSes.Tests;

public sealed class NotificationsAwsSesActivitySourceTests
{
    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsAwsSesActivitySource.Source.Name
            .ShouldBe("Granit.Notifications.AwsSes");

    [Fact]
    public void Operations_SendEmail_HasCorrectValue() =>
        NotificationsAwsSesActivitySource.Operations.SendEmail
            .ShouldBe("ses.send-email");
}
