using System.Reflection;
using Granit.Payments.Wolverine.Handlers;
using Shouldly;
using Xunit;

namespace Granit.Payments.Wolverine.Tests;

public sealed class PaymentsWolverineHandlerTests
{
    // -------------------------------------------------------------------------
    // InvoiceFinalizedHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void InvoiceFinalizedHandler_ShouldBeInternalStaticPartial()
    {
        Type handlerType = typeof(InvoiceFinalizedHandler);

        handlerType.IsAbstract.ShouldBeTrue("static classes are abstract");
        handlerType.IsSealed.ShouldBeTrue("static classes are sealed");
        handlerType.IsNotPublic.ShouldBeTrue("handler should be internal");
    }

    [Fact]
    public void InvoiceFinalizedHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(InvoiceFinalizedHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void InvoiceFinalizedHandler_HandleAsync_FirstParameterShouldBeInvoiceFinalizedEto()
    {
        MethodInfo? method = typeof(InvoiceFinalizedHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("InvoiceFinalizedEto");
    }

    [Fact]
    public void InvoiceFinalizedHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(InvoiceFinalizedHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }
}
