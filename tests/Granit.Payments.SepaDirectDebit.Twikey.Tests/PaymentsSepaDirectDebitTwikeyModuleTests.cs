using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Twikey.Tests;

public sealed class PaymentsSepaDirectDebitTwikeyModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitPaymentsSepaDirectDebitTwikeyModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitPaymentsSepaDirectDebitTwikeyModule");
    }
}
