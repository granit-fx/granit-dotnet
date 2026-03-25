using Granit.Authentication.JwtBearer;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Tests;

public sealed class GranitAuthenticationGoogleCloudModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitAuthenticationGoogleCloudModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnJwtBearerModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitAuthenticationGoogleCloudModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitJwtBearerModule));
    }
}
