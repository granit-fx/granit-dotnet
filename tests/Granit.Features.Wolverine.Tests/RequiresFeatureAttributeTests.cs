using Granit.Features.Wolverine.Attributes;
using Shouldly;
using Xunit;

namespace Granit.Features.Wolverine.Tests;

public sealed class RequiresFeatureAttributeTests
{
    [Fact]
    public void AttributeUsage_AllowsMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RequiresFeatureAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeTrue();
    }

    [Fact]
    public void AttributeUsage_IsInherited()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RequiresFeatureAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.Inherited.ShouldBeTrue();
    }

    [Fact]
    public void AttributeUsage_TargetsClassAndMethod()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RequiresFeatureAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Method);
    }

    [RequiresFeature("App.FeatureA")]
    [RequiresFeature("App.FeatureB")]
    private sealed class MultiAttributeTarget;

    [Fact]
    public void MultipleAttributes_AreAllRetrievable()
    {
        object[] attributes = typeof(MultiAttributeTarget)
            .GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true);

        attributes.Length.ShouldBe(2);
        IEnumerable<string> featureNames = attributes.Cast<RequiresFeatureAttribute>()
            .Select(a => a.FeatureName);
        featureNames.ShouldContain("App.FeatureA");
        featureNames.ShouldContain("App.FeatureB");
    }
}
