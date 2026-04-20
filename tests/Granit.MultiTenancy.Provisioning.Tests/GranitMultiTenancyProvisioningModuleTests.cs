using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Provisioning.Tests;

public sealed class GranitMultiTenancyProvisioningModuleTests
{
    [Fact]
    public void Module_ShouldDependOnMultiTenancyModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitMultiTenancyProvisioningModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitMultiTenancyModule));
    }

    [Fact]
    public void Module_ShouldDependOnHostingModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitMultiTenancyProvisioningModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreHostingModule));
    }

    [Fact]
    public void Module_ShouldDependOnWolverineModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitMultiTenancyProvisioningModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(global::Granit.Wolverine.GranitWolverineModule));
    }

    [Fact]
    public void Module_ShouldBeSealed() =>
        typeof(GranitMultiTenancyProvisioningModule).IsSealed.ShouldBeTrue();
}
