using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class GranitIdentityFederatedEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitIdentityFederatedEntityFrameworkCoreModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnAttribute()
    {
        Type moduleType = typeof(GranitIdentityFederatedEntityFrameworkCoreModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
    }

    [Fact]
    public void Module_DependsOnGranitIdentityModule()
    {
        Type moduleType = typeof(GranitIdentityFederatedEntityFrameworkCoreModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependencyTypes = attrs.SelectMany(a => a.DependedTypes).ToArray();
        dependencyTypes.ShouldContain(typeof(GranitIdentityFederatedModule));
    }
}
