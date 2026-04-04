using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Tests;

public sealed class PaymentsSepaDirectDebitModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitPaymentsSepaDirectDebitModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitPaymentsSepaDirectDebitModule");
    }
}
