using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class TimelineReactionNotificationTypeTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        TimelineReactionNotificationType instance = TimelineReactionNotificationType.Instance;

        instance.ShouldNotBeNull();
        instance.ShouldBeSameAs(TimelineReactionNotificationType.Instance);
    }

    [Fact]
    public void Name_is_timeline_reaction_toggled() => TimelineReactionNotificationType.Instance.Name.ShouldBe("timeline.reaction_toggled");

    [Fact]
    public void DefaultChannels_Contains_InApp() => TimelineReactionNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.InApp);

    [Fact]
    public void DefaultChannels_Contains_SignalR() => TimelineReactionNotificationType.Instance.DefaultChannels.ShouldContain(NotificationChannels.SignalR);

    [Fact]
    public void DefaultChannels_DoesNotContain_Email() => TimelineReactionNotificationType.Instance.DefaultChannels.ShouldNotContain(NotificationChannels.Email);

    [Fact]
    public void DefaultChannels_HasExactlyTwo() => TimelineReactionNotificationType.Instance.DefaultChannels.Count.ShouldBe(2);
}
