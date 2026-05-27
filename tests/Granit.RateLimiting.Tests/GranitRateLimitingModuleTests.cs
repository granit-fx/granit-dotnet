using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class GranitRateLimitingModuleTests
{
    [Fact]
    public void Module_HasDependsOnAttribute()
    {
        object[] attributes = typeof(GranitRateLimitingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        attributes.ShouldNotBeEmpty();
    }

    [Fact]
    public void Module_IsGranitModule()
    {
        GranitRateLimitingModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() => typeof(GranitRateLimitingModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnFeatures()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitRateLimitingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = attrs.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldContain(typeof(Granit.Features.GranitFeaturesModule));
    }

    [Fact]
    public void Module_DependsOnSecurity()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitRateLimitingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = attrs.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldNotContain(t => t.Name == "GranitSecurityModule",
            "GranitSecurityModule was dissolved — it should no longer appear in DependsOn");
    }
}
