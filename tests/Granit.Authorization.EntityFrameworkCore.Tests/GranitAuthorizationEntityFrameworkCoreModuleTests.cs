using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class GranitAuthorizationEntityFrameworkCoreModuleTests
{
    [Fact]
    public void InheritsFromGranitModule()
    {
        GranitAuthorizationEntityFrameworkCoreModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void DependsOn_GranitAuthorizationModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitAuthorizationModule));
    }

    [Fact]
    public void DependsOn_GranitPersistenceModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(Granit.Persistence.EntityFrameworkCore.GranitPersistenceEntityFrameworkCoreModule));
    }
}
