using Granit.Modularity;
using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class GranitTimelineNotificationsModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitTimelineNotificationsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() => typeof(GranitTimelineNotificationsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitNotificationsModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineNotificationsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitNotificationsAbstractionsModule));
    }

    [Fact]
    public void Module_DependsOn_GranitTimelineModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineNotificationsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitTimelineModule));
    }
}
