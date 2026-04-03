using Xunit;

namespace Granit.Invoicing.Notifications.Tests;

public sealed class GranitInvoicingNotificationsModuleTests
{
    [Fact]
    public void Module_can_be_instantiated()
    {
        var module = new GranitInvoicingNotificationsModule();

        Assert.NotNull(module);
    }
}
