using System.Reflection;
using Granit.CustomerBalance.Wolverine.Handlers;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Wolverine.Tests;

public sealed class CustomerBalanceWolverineHandlerTests
{
    [Fact]
    public void OverpaymentCreditHandler_ShouldBeInternalStaticPartial()
    {
        Type handlerType = typeof(OverpaymentCreditHandler);

        handlerType.IsAbstract.ShouldBeTrue("static classes are abstract");
        handlerType.IsSealed.ShouldBeTrue("static classes are sealed");
        handlerType.IsNotPublic.ShouldBeTrue("handler should be internal");
    }

    [Fact]
    public void OverpaymentCreditHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(OverpaymentCreditHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }
}
