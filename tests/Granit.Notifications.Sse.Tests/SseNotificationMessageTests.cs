using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseNotificationMessageTests
{
    [Fact]
    public void Record_HasCorrectDefaults()
    {
        SseNotificationMessage message = new();

        message.NotificationId.ShouldBe(Guid.Empty);
        message.NotificationTypeName.ShouldBe(string.Empty);
        message.Severity.ShouldBe(NotificationSeverity.Info);
        message.RelatedEntityType.ShouldBeNull();
        message.RelatedEntityId.ShouldBeNull();
    }
}
