using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class TimelineCommentNotificationTypeTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        TimelineCommentNotificationType instance = TimelineCommentNotificationType.Instance;

        instance.ShouldNotBeNull();
        instance.ShouldBeSameAs(TimelineCommentNotificationType.Instance);
    }

    [Fact]
    public void Name_is_timeline_comment_posted() => TimelineCommentNotificationType.Instance.Name.ShouldBe("timeline.comment_posted");

    [Fact]
    public void DefaultChannels_Contains_InApp() => TimelineCommentNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.InApp);

    [Fact]
    public void DefaultChannels_Contains_SignalR() => TimelineCommentNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.SignalR);

    [Fact]
    public void DefaultChannels_DoesNotContain_Email() => TimelineCommentNotificationType.Instance.DefaultChannels.ShouldNotContain(NotificationChannels.Email);

    [Fact]
    public void DefaultChannels_HasExactlyTwo() => TimelineCommentNotificationType.Instance.DefaultChannels.Count.ShouldBe(2);
}
