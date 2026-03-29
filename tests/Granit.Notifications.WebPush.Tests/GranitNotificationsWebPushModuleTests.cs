using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class GranitNotificationsWebPushModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsWebPushModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsWebPushModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnGranitNotificationsModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitNotificationsWebPushModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitNotificationsAbstractionsModule)));
    }
}
