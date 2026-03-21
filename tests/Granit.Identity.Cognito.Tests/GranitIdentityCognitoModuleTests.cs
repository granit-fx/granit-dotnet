using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Identity.Cognito.Tests;

public sealed class GranitIdentityCognitoModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitIdentityCognitoModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnAttribute()
    {
        Type moduleType = typeof(GranitIdentityCognitoModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
    }

    [Fact]
    public void Module_DependsOnGranitIdentityModule()
    {
        Type moduleType = typeof(GranitIdentityCognitoModule);

        DependsOnAttribute[] attrs = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependencyTypes = attrs.SelectMany(a => a.DependedTypes).ToArray();
        dependencyTypes.ShouldContain(typeof(GranitIdentityModule));
    }
}
