using System.Reflection;
using Granit.Invoicing.Wolverine.Handlers;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Wolverine.Tests;

public sealed class InvoicingWolverineHandlerTests
{
    // -------------------------------------------------------------------------
    // CreateInvoiceHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateInvoiceHandler_ShouldBeInternalStaticPartial()
    {
        Type handlerType = typeof(CreateInvoiceHandler);

        handlerType.IsAbstract.ShouldBeTrue("static classes are abstract");
        handlerType.IsSealed.ShouldBeTrue("static classes are sealed");
        handlerType.IsNotPublic.ShouldBeTrue("handler should be internal");
    }

    [Fact]
    public void CreateInvoiceHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(CreateInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void CreateInvoiceHandler_HandleAsync_FirstParameterShouldBeCreateInvoiceCommand()
    {
        MethodInfo? method = typeof(CreateInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("CreateInvoiceCommand");
    }

    [Fact]
    public void CreateInvoiceHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(CreateInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }
}
