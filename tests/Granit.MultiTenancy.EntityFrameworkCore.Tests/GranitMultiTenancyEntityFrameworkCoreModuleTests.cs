using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

public sealed class GranitMultiTenancyEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitMultiTenancyModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMultiTenancyEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitMultiTenancyModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceEntityFrameworkCoreModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMultiTenancyEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitMultiTenancyEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();
}
