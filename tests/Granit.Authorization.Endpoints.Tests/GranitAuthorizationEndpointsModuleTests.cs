using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class GranitAuthorizationEndpointsModuleTests
{
    [Fact]
    public void InheritsFromGranitModule()
    {
        GranitAuthorizationEndpointsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void HasDependsOnAttributes()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
    }

    [Fact]
    public void DependsOn_GranitAuthorizationModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitAuthorizationModule));
    }
}
