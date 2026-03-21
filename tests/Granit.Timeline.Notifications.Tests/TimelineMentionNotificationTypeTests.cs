using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class TimelineMentionNotificationTypeTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        TimelineMentionNotificationType instance = TimelineMentionNotificationType.Instance;

        instance.ShouldNotBeNull();
        instance.ShouldBeSameAs(TimelineMentionNotificationType.Instance);
    }

    [Fact]
    public void Name_is_timeline_user_mentioned() => TimelineMentionNotificationType.Instance.Name.ShouldBe("timeline.user_mentioned");

    [Fact]
    public void DefaultChannels_Contains_InApp() => TimelineMentionNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.InApp);

    [Fact]
    public void DefaultChannels_Contains_SignalR() => TimelineMentionNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.SignalR);

    [Fact]
    public void DefaultChannels_Contains_Email() => TimelineMentionNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.Email);

    [Fact]
    public void DefaultChannels_HasExactlyThree() => TimelineMentionNotificationType.Instance.DefaultChannels.Count.ShouldBe(3);
}
