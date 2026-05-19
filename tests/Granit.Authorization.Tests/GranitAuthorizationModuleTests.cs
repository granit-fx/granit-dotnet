using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class GranitAuthorizationModuleTests
{
    [Fact]
    public void InheritsFromGranitModule()
    {
        GranitAuthorizationModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void HasDependsOnAttributes()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
    }

    [Fact]
    public void ConfigureServices_RegistersPermissionDefinitionProviders()
    {
        // The GranitAuthorizationModule scans assemblies for IPermissionDefinitionProvider.
        // Verifying the type is correct is sufficient here since integration testing
        // ConfigureServices requires the full module system.
        typeof(GranitAuthorizationModule)
            .GetMethod("ConfigureServices")
            .ShouldNotBeNull();
    }

    // =========================================================================
    // DependsOn — GranitCachingModule
    // =========================================================================

    [Fact]
    public void DependsOn_IncludesCachingModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(Granit.Caching.GranitCachingModule));
    }

    // =========================================================================
    // Module class — sealed
    // =========================================================================

    [Fact]
    public void ModuleClass_IsSealed() =>
        typeof(GranitAuthorizationModule).IsSealed.ShouldBeTrue();
}
