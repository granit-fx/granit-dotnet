using Granit.Modularity;
using Granit.Workflow;
using Shouldly;
using Xunit;

namespace Granit.Catalog.Tests;

public sealed class GranitCatalogModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitCatalogModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitCatalogModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitWorkflowModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitCatalogModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitWorkflowModule));
    }
}
