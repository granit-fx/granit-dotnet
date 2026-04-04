using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.GoCardless.Tests;

public sealed class PaymentsSepaDirectDebitGoCardlessModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitPaymentsSepaDirectDebitGoCardlessModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitPaymentsSepaDirectDebitGoCardlessModule");
    }
}
