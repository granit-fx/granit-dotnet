using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Catalog.EntityFrameworkCore.Tests;

public sealed class GranitCatalogEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitCatalogEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitCatalogEntityFrameworkCoreModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitCatalogModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitCatalogEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitCatalogModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceEntityFrameworkCoreModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitCatalogEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }
}
