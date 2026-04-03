using Xunit;

namespace Granit.Subscriptions.Notifications.Tests;

public sealed class GranitSubscriptionsNotificationsModuleTests
{
    [Fact]
    public void Module_can_be_instantiated()
    {
        var module = new GranitSubscriptionsNotificationsModule();

        Assert.NotNull(module);
    }
}
