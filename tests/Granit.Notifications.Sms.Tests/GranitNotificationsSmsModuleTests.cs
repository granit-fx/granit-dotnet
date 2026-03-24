using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.Tests;

public sealed class GranitNotificationsSmsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsSmsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsSmsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnGranitNotificationsModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitNotificationsSmsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitNotificationsModule)));
    }
}
