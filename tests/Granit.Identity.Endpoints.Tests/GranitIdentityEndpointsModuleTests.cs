using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

public sealed class GranitIdentityEndpointsModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitIdentityEndpointsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnAttribute()
    {
        Type moduleType = typeof(GranitIdentityEndpointsModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
    }

    [Fact]
    public void Module_DependsOnGranitIdentityModule()
    {
        Type moduleType = typeof(GranitIdentityEndpointsModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependencyTypes = attrs.SelectMany(a => a.DependedTypes).ToArray();
        dependencyTypes.ShouldContain(typeof(GranitIdentityModule));
    }
}
