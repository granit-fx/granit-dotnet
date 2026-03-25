using Granit.Authentication.JwtBearer;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Tests;

public sealed class GranitAuthenticationJwtBearerGoogleCloudModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitAuthenticationJwtBearerGoogleCloudModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnJwtBearerModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitAuthenticationJwtBearerGoogleCloudModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitAuthenticationJwtBearerModule));
    }
}
