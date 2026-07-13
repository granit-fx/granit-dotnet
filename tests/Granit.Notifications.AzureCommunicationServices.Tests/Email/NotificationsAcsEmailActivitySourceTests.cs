using Granit.Notifications.AzureCommunicationServices.Email.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Email.Tests;

public sealed class NotificationsAcsEmailActivitySourceTests
{
    [Fact]
    public void Source_HasCorrectName() =>
        NotificationsAcsEmailActivitySource.Source.Name
            .ShouldBe("Granit.Notifications.AzureCommunicationServices.Email");

    [Fact]
    public void Operations_SendEmail_HasCorrectValue() =>
        NotificationsAcsEmailActivitySource.Operations.SendEmail
            .ShouldBe("acs-email.send");

    [Fact]
    public void Tags_To_HasCorrectValue() =>
        NotificationsAcsEmailActivitySource.Tags.To
            .ShouldBe("acs.email.to");

    [Fact]
    public void Tags_SubjectLength_HasCorrectValue() =>
        NotificationsAcsEmailActivitySource.Tags.SubjectLength
            .ShouldBe("acs.email.subject_length");
}
