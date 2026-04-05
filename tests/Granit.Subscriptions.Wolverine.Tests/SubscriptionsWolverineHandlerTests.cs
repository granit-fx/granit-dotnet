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
    public void SubscriptionProviderSyncHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(SubscriptionProviderSyncHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
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
    public void UsageSummaryReadyHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(UsageSummaryReadyHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
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
