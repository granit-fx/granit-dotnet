using Granit.Modularity;
using Granit.Subscriptions;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Builtin.Tests;

public sealed class GranitSubscriptionsBuiltinModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitSubscriptionsBuiltinModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitSubscriptionsBuiltinModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitSubscriptionsModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitSubscriptionsBuiltinModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitSubscriptionsModule));
    }

    [Fact]
    public void BuiltinSubscriptionProvider_Exists()
    {
        Type? type = typeof(GranitSubscriptionsBuiltinModule).Assembly
            .GetType("Granit.Subscriptions.Builtin.Internal.BuiltinSubscriptionProvider");

        type.ShouldNotBeNull();
    }

    [Fact]
    public void BuiltinSubscriptionProvider_Implements_ISubscriptionProvider()
    {
        Type? type = typeof(GranitSubscriptionsBuiltinModule).Assembly
            .GetType("Granit.Subscriptions.Builtin.Internal.BuiltinSubscriptionProvider");

        type.ShouldNotBeNull();
        type.IsAssignableTo(typeof(ISubscriptionProvider)).ShouldBeTrue();
    }

    [Fact]
    public void BuiltinSubscriptionProvider_Name_IsInternal()
    {
        Type? type = typeof(GranitSubscriptionsBuiltinModule).Assembly
            .GetType("Granit.Subscriptions.Builtin.Internal.BuiltinSubscriptionProvider");

        type.ShouldNotBeNull();
        var instance = (ISubscriptionProvider)Activator.CreateInstance(type)!;

        instance.ShouldNotBeNull();
        instance.Name.ShouldBe("internal");
    }

    [Fact]
    public void BuiltinSubscriptionProvider_Capabilities_IsNone()
    {
        Type? type = typeof(GranitSubscriptionsBuiltinModule).Assembly
            .GetType("Granit.Subscriptions.Builtin.Internal.BuiltinSubscriptionProvider");

        type.ShouldNotBeNull();
        var instance = (ISubscriptionProvider)Activator.CreateInstance(type)!;

        instance.ShouldNotBeNull();
        instance.Capabilities.ShouldBe(SubscriptionProviderCapabilities.None);
    }
}
