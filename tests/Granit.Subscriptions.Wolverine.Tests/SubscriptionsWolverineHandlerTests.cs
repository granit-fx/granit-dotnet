using System.Reflection;
using Granit.Subscriptions.Wolverine.Handlers;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Wolverine.Tests;

public sealed class SubscriptionsWolverineHandlerTests
{
    // -------------------------------------------------------------------------
    // SubscriptionProviderSyncHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void SubscriptionProviderSyncHandler_ShouldBeInternalStaticPartial()
    {
        Type handlerType = typeof(SubscriptionProviderSyncHandler);

        handlerType.IsAbstract.ShouldBeTrue("static classes are abstract");
        handlerType.IsSealed.ShouldBeTrue("static classes are sealed");
        handlerType.IsNotPublic.ShouldBeTrue("handler should be internal");
    }

    [Fact]
    public void SubscriptionProviderSyncHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(SubscriptionProviderSyncHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void SubscriptionProviderSyncHandler_HandleAsync_FirstParameterShouldBeSubscriptionCancelledEto()
    {
        MethodInfo? method = typeof(SubscriptionProviderSyncHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("SubscriptionCancelledEto");
    }

    [Fact]
    public void SubscriptionProviderSyncHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(SubscriptionProviderSyncHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }

    // -------------------------------------------------------------------------
    // UsageSummaryReadyHandler
    // -------------------------------------------------------------------------

    [Fact]
    public void UsageSummaryReadyHandler_ShouldBeInternalStaticPartial()
    {
        Type handlerType = typeof(UsageSummaryReadyHandler);

        handlerType.IsAbstract.ShouldBeTrue("static classes are abstract");
        handlerType.IsSealed.ShouldBeTrue("static classes are sealed");
        handlerType.IsNotPublic.ShouldBeTrue("handler should be internal");
    }

    [Fact]
    public void UsageSummaryReadyHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(UsageSummaryReadyHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void UsageSummaryReadyHandler_HandleAsync_FirstParameterShouldBeUsageSummaryReadyEto()
    {
        MethodInfo? method = typeof(UsageSummaryReadyHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThan(0);
        parameters[0].ParameterType.Name.ShouldBe("UsageSummaryReadyEto");
    }

    [Fact]
    public void UsageSummaryReadyHandler_HandleAsync_LastParameterShouldBeCancellationToken()
    {
        MethodInfo? method = typeof(UsageSummaryReadyHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters[^1].ParameterType.ShouldBe(typeof(CancellationToken));
    }
}
