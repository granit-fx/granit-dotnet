using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Abstractions.Tests;

public sealed class GranitNotificationsAbstractionsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsAbstractionsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
