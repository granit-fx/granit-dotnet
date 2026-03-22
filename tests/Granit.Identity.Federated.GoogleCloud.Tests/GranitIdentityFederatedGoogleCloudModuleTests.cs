using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GranitIdentityFederatedGoogleCloudModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitIdentityFederatedGoogleCloudModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnAttribute()
    {
        Type moduleType = typeof(GranitIdentityFederatedGoogleCloudModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
    }

    [Fact]
    public void Module_DependsOnGranitIdentityModule()
    {
        Type moduleType = typeof(GranitIdentityFederatedGoogleCloudModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependencyTypes = attrs.SelectMany(a => a.DependedTypes).ToArray();
        dependencyTypes.ShouldContain(typeof(GranitIdentityFederatedModule));
    }
}
