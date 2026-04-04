using Shouldly;
using Xunit;

namespace Granit.Invoicing.Odoo.Tests;

public sealed class InvoicingOdooModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitInvoicingOdooModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitInvoicingOdooModule");
    }
}
