using Shouldly;
using Xunit;

namespace Granit.Notifications.SignalR.Tests;

public sealed class SignalRNotificationMessageTests
{
    [Fact]
    public void Default_NotificationId_IsEmptyGuid() =>
        new SignalRNotificationMessage().NotificationId.ShouldBe(Guid.Empty);

    [Fact]
    public void Default_NotificationTypeName_IsEmpty() =>
        new SignalRNotificationMessage().NotificationTypeName.ShouldBe(string.Empty);

    [Fact]
    public void Default_Severity_IsInfo() =>
        new SignalRNotificationMessage().Severity.ShouldBe(NotificationSeverity.Info);

    [Fact]
    public void Record_IsSealed() =>
        typeof(SignalRNotificationMessage).IsSealed.ShouldBeTrue();
}
