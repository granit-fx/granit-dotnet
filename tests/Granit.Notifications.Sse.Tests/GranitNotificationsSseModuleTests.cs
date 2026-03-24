using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class GranitNotificationsSseModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsSseModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsSseModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnGranitNotificationsModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitNotificationsSseModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitNotificationsModule)));
    }
}
