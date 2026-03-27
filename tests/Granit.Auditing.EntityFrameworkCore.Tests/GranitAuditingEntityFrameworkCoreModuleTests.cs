// =============================================================================
// GranitAuditingEntityFrameworkCoreModuleTests - Module declaration
// =============================================================================
// Verifies:
//   - DependsOn includes GranitAuditingModule, GranitCachingModule, GranitPersistenceModule
//   - Module class is sealed
//   - Module inherits from GranitModule
// =============================================================================

using Granit.Auditing;
using Granit.Caching;
using Granit.Modularity;
using Granit.Persistence;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests;

public sealed class GranitAuditingEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitAuditingEntityFrameworkCoreModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed()
    {
        typeof(GranitAuditingEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Module_DependsOnGranitAuditingModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitAuditingEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitAuditingModule));
    }

    [Fact]
    public void Module_DependsOnGranitCachingModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitAuditingEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .ToArray();

        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitCachingModule));
    }

    [Fact]
    public void Module_DependsOnGranitPersistenceModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitAuditingEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .ToArray();

        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceModule));
    }

    [Fact]
    public void Module_HasExactlyThreeDependencies()
    {
        DependsOnAttribute[] attributes = typeof(GranitAuditingEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .ToArray();

        attributes.SelectMany(a => a.DependedTypes).Count().ShouldBe(3);
    }
}
