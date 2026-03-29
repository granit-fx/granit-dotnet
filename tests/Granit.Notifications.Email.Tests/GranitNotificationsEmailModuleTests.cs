using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class GranitNotificationsEmailModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsEmailModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsEmailModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnGranitNotificationsModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitNotificationsEmailModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitNotificationsAbstractionsModule)));
    }
}
