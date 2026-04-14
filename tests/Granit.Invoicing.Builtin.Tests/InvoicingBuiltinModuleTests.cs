using Shouldly;
using Xunit;

namespace Granit.Invoicing.Builtin.Tests;

public sealed class InvoicingBuiltinModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitInvoicingBuiltinModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitInvoicingBuiltinModule");
    }
}
