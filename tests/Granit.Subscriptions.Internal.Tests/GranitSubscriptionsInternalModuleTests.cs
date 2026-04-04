using Granit.Modularity;
using Granit.Subscriptions;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Internal.Tests;

public sealed class GranitSubscriptionsInternalModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitSubscriptionsInternalModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitSubscriptionsInternalModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitSubscriptionsModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitSubscriptionsInternalModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitSubscriptionsModule));
    }

    [Fact]
    public void InternalSubscriptionProvider_Exists()
    {
        Type? type = typeof(GranitSubscriptionsInternalModule).Assembly
            .GetType("Granit.Subscriptions.Internal.Internal.InternalSubscriptionProvider");

        type.ShouldNotBeNull();
    }

    [Fact]
    public void InternalSubscriptionProvider_Implements_ISubscriptionProvider()
    {
        Type? type = typeof(GranitSubscriptionsInternalModule).Assembly
            .GetType("Granit.Subscriptions.Internal.Internal.InternalSubscriptionProvider");

        type.ShouldNotBeNull();
        type.IsAssignableTo(typeof(ISubscriptionProvider)).ShouldBeTrue();
    }

    [Fact]
    public void InternalSubscriptionProvider_Name_IsInternal()
    {
        Type? type = typeof(GranitSubscriptionsInternalModule).Assembly
            .GetType("Granit.Subscriptions.Internal.Internal.InternalSubscriptionProvider");

        type.ShouldNotBeNull();
        var instance = (ISubscriptionProvider)Activator.CreateInstance(type)!;

        instance.ShouldNotBeNull();
        instance.Name.ShouldBe("internal");
    }

    [Fact]
    public void InternalSubscriptionProvider_Capabilities_IsNone()
    {
        Type? type = typeof(GranitSubscriptionsInternalModule).Assembly
            .GetType("Granit.Subscriptions.Internal.Internal.InternalSubscriptionProvider");

        type.ShouldNotBeNull();
        var instance = (ISubscriptionProvider)Activator.CreateInstance(type)!;

        instance.ShouldNotBeNull();
        instance.Capabilities.ShouldBe(SubscriptionProviderCapabilities.None);
    }
}
