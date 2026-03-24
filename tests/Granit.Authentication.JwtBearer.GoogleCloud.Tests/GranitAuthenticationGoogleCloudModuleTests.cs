using Granit.Authentication.JwtBearer;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Tests;

public sealed class GranitJwtBearerGoogleCloudModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitJwtBearerGoogleCloudModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnJwtBearerModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitJwtBearerGoogleCloudModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitJwtBearerModule));
    }
}
