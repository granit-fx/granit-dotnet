using Granit.Features;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Features.Tests;

public sealed class GranitSubscriptionsFeaturesModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitSubscriptionsFeaturesModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitSubscriptionsFeaturesModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitFeaturesModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitSubscriptionsFeaturesModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitFeaturesModule));
    }

    [Fact]
    public void Module_DependsOn_GranitSubscriptionsModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitSubscriptionsFeaturesModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitSubscriptionsModule));
    }

    [Fact]
    public void SubscriptionPlanIdProvider_Exists()
    {
        Type? type = typeof(GranitSubscriptionsFeaturesModule).Assembly
            .GetType("Granit.Subscriptions.Features.Internal.SubscriptionPlanIdProvider");

        type.ShouldNotBeNull();
    }

    [Fact]
    public void SubscriptionPlanIdProvider_Implements_IPlanIdProvider()
    {
        Type? type = typeof(GranitSubscriptionsFeaturesModule).Assembly
            .GetType("Granit.Subscriptions.Features.Internal.SubscriptionPlanIdProvider");

        type.ShouldNotBeNull();
        type.IsAssignableTo(typeof(Granit.Features.Plans.IPlanIdProvider)).ShouldBeTrue();
    }

    [Fact]
    public void PlanFeatureValueStore_Exists()
    {
        Type? type = typeof(GranitSubscriptionsFeaturesModule).Assembly
            .GetType("Granit.Subscriptions.Features.Internal.PlanFeatureValueStore");

        type.ShouldNotBeNull();
    }

    [Fact]
    public void PlanFeatureValueStore_Implements_IPlanFeatureStore()
    {
        Type? type = typeof(GranitSubscriptionsFeaturesModule).Assembly
            .GetType("Granit.Subscriptions.Features.Internal.PlanFeatureValueStore");

        type.ShouldNotBeNull();
        type.IsAssignableTo(typeof(Granit.Features.Plans.IPlanFeatureStore)).ShouldBeTrue();
    }
}
