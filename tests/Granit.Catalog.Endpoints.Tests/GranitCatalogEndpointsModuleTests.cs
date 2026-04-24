using Granit.Catalog;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Catalog.Endpoints.Tests;

public sealed class GranitCatalogEndpointsModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitCatalogEndpointsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitCatalogEndpointsModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitCatalogModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitCatalogEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitCatalogModule));
    }
}
