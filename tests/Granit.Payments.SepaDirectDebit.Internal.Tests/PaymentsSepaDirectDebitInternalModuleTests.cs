using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Internal.Tests;

public sealed class PaymentsSepaDirectDebitInternalModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitPaymentsSepaDirectDebitInternalModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitPaymentsSepaDirectDebitInternalModule");
    }
}
