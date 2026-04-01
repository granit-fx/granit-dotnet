using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Notifications.Tests;

public sealed class ModuleTests
{
    [Fact]
    public void Module_can_be_instantiated() =>
        new GranitIdentityLocalNotificationsModule().ShouldNotBeNull();
}
