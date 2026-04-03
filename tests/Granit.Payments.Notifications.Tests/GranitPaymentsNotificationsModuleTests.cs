using Xunit;

namespace Granit.Payments.Notifications.Tests;

public sealed class GranitPaymentsNotificationsModuleTests
{
    [Fact]
    public void Module_can_be_instantiated()
    {
        var module = new GranitPaymentsNotificationsModule();

        Assert.NotNull(module);
    }
}
