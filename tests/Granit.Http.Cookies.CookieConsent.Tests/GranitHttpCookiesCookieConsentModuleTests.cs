using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.CookieConsent.Tests;

public sealed class GranitHttpCookiesCookieConsentModuleTests
{
    [Fact]
    public void Module_DependsOnGranitHttpCookiesModule()
    {
        var attribute = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitHttpCookiesCookieConsentModule), typeof(DependsOnAttribute));

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitHttpCookiesModule));
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitHttpCookiesCookieConsentModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
