using System.Reflection;
using Granit.CustomerBalance.Wolverine.Handlers;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Wolverine.Tests;

public sealed class CustomerBalanceWolverineHandlerTests
{
    [Fact]
    public void OverpaymentCreditHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(OverpaymentCreditHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
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
