using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Builtin.Tests;

public sealed class PaymentsSepaDirectDebitBuiltinModuleTests
{
    [Fact]
    public void Module_should_exist()
    {
        Type moduleType = typeof(GranitPaymentsSepaDirectDebitBuiltinModule);
        moduleType.ShouldNotBeNull();
        moduleType.Name.ShouldBe("GranitPaymentsSepaDirectDebitBuiltinModule");
    }
}
