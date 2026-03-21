using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

public sealed class GranitHttpCookiesEndpointsModuleTests
{
    [Fact]
    public void Module_DependsOnApiDocumentationAndCookies()
    {
        var attribute = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitHttpCookiesEndpointsModule), typeof(DependsOnAttribute));

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitHttpApiDocumentationModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitHttpCookiesModule));
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitHttpCookiesEndpointsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
