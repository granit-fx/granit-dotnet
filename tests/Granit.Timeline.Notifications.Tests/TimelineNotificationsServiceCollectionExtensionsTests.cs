using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Security;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Extensions;
using Granit.Timeline.Notifications.Extensions;
using Granit.Timeline.Notifications.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class TimelineNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTimelineNotifications_ReplacesFollowerService()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitTimeline();
        services.AddGranitTimelineNotifications();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITimelineFollowerService followerService = sp.GetRequiredService<ITimelineFollowerService>();

        followerService.ShouldBeOfType<NotificationBackedFollowerService>();
    }

    [Fact]
    public void AddGranitTimelineNotifications_ReplacesNotifier()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitTimeline();
        services.AddGranitTimelineNotifications();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITimelineNotifier notifier = sp.GetRequiredService<ITimelineNotifier>();

        notifier.ShouldBeOfType<NotificationBackedNotifier>();
    }

    private static void AddRequiredDependencies(ServiceCollection services)
    {
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IGuidGenerator>());
        services.AddSingleton(Substitute.For<ICurrentUserService>());
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        services.AddSingleton(Substitute.For<INotificationSubscriptionReader>());
        services.AddSingleton(Substitute.For<INotificationSubscriptionWriter>());
        services.AddSingleton(Substitute.For<INotificationPublisher>());
    }
}
