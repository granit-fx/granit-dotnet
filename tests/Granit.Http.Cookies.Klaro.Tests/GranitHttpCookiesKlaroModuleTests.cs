using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Klaro.Tests;

public sealed class GranitHttpCookiesKlaroModuleTests
{
    [Fact]
    public void Module_DependsOnGranitHttpCookiesModule()
    {
        var attribute = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitHttpCookiesKlaroModule), typeof(DependsOnAttribute));

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitHttpCookiesModule));
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitHttpCookiesKlaroModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
