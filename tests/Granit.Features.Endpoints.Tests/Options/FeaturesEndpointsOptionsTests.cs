using Granit.Features.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests.Options;

public sealed class FeaturesEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        FeaturesEndpointsOptions.SectionName.ShouldBe("Features:Endpoints");

    [Fact]
    public void Defaults_RoutePrefix_IsFeatures()
    {
        FeaturesEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("features");
    }

    [Fact]
    public void Defaults_TagName_IsFeatures()
    {
        FeaturesEndpointsOptions options = new();

        options.TagName.ShouldBe("Features");
    }

    [Fact]
    public void RoutePrefix_CanBeOverridden()
    {
        FeaturesEndpointsOptions options = new() { RoutePrefix = "api/features" };

        options.RoutePrefix.ShouldBe("api/features");
    }

    [Fact]
    public void TagName_CanBeOverridden()
    {
        FeaturesEndpointsOptions options = new() { TagName = "FeatureManagement" };

        options.TagName.ShouldBe("FeatureManagement");
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(FeaturesEndpointsOptions).IsSealed.ShouldBeTrue();
}
