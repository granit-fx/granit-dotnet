using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SignalR.Tests;

public sealed class GranitNotificationsSignalRModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsSignalRModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsSignalRModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnGranitNotificationsModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitNotificationsSignalRModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitNotificationsModule)));
    }
}
