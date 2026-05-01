using Granit.Http.Cookies.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

public sealed class CookieConsentEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_Default_ShouldBeCookies() =>
        new CookieConsentEndpointsOptions().RoutePrefix.ShouldBe("cookies");

    [Fact]
    public void TagName_Default_ShouldBeCookieConsent() =>
        new CookieConsentEndpointsOptions().TagName.ShouldBe("Cookie Consent");
}
