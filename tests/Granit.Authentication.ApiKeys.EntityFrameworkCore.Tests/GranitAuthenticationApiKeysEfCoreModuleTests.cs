using Granit.Authentication.ApiKeys.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class GranitAuthenticationApiKeysEfCoreModuleTests
{
    [Fact]
    public void Module_IsGranitModule() =>
        typeof(GranitAuthenticationApiKeysEntityFrameworkCoreModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOnApiKeysModule()
    {
        DependsOnAttribute[] deps = typeof(GranitAuthenticationApiKeysEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        deps.SelectMany(d => d.DependedTypes)
            .ShouldContain(typeof(GranitAuthenticationApiKeysModule));
    }

    [Fact]
    public void Module_DependsOnPersistenceModule()
    {
        DependsOnAttribute[] deps = typeof(GranitAuthenticationApiKeysEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        deps.SelectMany(d => d.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }
}
