using Shouldly;
using Xunit;

namespace Granit.Invoicing.Internal.Tests;

public sealed class InvoicingInternalModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitInvoicingInternalModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitInvoicingInternalModule");
    }
}
