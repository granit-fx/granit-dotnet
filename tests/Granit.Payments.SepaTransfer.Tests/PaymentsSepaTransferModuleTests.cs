using Shouldly;
using Xunit;

namespace Granit.Payments.SepaTransfer.Tests;

public sealed class PaymentsSepaTransferModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitPaymentsSepaTransferModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitPaymentsSepaTransferModule");
    }
}
