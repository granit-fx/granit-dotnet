using Granit.Http.Cookies.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests.Options;

public sealed class CookieConsentEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        CookieConsentEndpointsOptions.SectionName.ShouldBe("Http:Cookies:Endpoints");
}
