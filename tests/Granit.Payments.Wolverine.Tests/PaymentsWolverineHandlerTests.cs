using System.Reflection;
using Granit.Payments.Wolverine.Handlers;
using Shouldly;
using Xunit;

namespace Granit.Payments.Wolverine.Tests;

public sealed class PaymentsWolverineHandlerTests
{
    // -------------------------------------------------------------------------
    // AutoChargeOnInvoiceHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void AutoChargeOnInvoiceHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(AutoChargeOnInvoiceHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void AutoChargeOnInvoiceHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(AutoChargeOnInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void AutoChargeOnInvoiceHandler_HandleAsync_FirstParameterShouldBeInvoiceFinalizedEto()
    {
        MethodInfo? method = typeof(AutoChargeOnInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("InvoiceFinalizedEto");
    }

    [Fact]
    public void AutoChargeOnInvoiceHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(AutoChargeOnInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }

    [Fact]
    public void AutoChargeOnInvoiceHandler_HandleAsync_ShouldBeThinPassThrough()
    {
        MethodInfo? method = typeof(AutoChargeOnInvoiceHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBe(3, "handler should only take (eto, service, ct)");
    }

    // -------------------------------------------------------------------------
    // ProcessWebhookCommandHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void ProcessWebhookCommandHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(ProcessWebhookCommandHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void ProcessWebhookCommandHandler_HandleAsync_ShouldBeThinPassThrough()
    {
        MethodInfo? method = typeof(ProcessWebhookCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBe(3, "handler should only take (command, service, ct)");
    }

    // -------------------------------------------------------------------------
    // InitiatePaymentCommandHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void InitiatePaymentCommandHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(InitiatePaymentCommandHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void InitiatePaymentCommandHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(InitiatePaymentCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void InitiatePaymentCommandHandler_HandleAsync_FirstParameterShouldBeInitiatePaymentCommand()
    {
        MethodInfo? method = typeof(InitiatePaymentCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("InitiatePaymentCommand");
    }

    [Fact]
    public void InitiatePaymentCommandHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(InitiatePaymentCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }

    // -------------------------------------------------------------------------
    // RequestRefundCommandHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void RequestRefundCommandHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(RequestRefundCommandHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void RequestRefundCommandHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(RequestRefundCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void RequestRefundCommandHandler_HandleAsync_FirstParameterShouldBeRequestRefundCommand()
    {
        MethodInfo? method = typeof(RequestRefundCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("RequestRefundCommand");
    }

    [Fact]
    public void RequestRefundCommandHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(RequestRefundCommandHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }
}
