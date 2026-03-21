using Granit.Http.Cookies.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

public sealed class CookieConsentConfigResponseTests
{
    [Fact]
    public void CookieConsentConfigResponse_Constructor_SetsProperties()
    {
        List<CookieDefinitionResponse> cookies =
        [
            new("session", "strictly_necessary", 1, "Session management"),
        ];
        List<ThirdPartyServiceResponse> services =
        [
            new("matomo", "analytics", ["^_pk_"]),
        ];

        CookieConsentConfigResponse response = new(cookies, services);

        response.Cookies.ShouldBe(cookies);
        response.Services.ShouldBe(services);
    }

    [Fact]
    public void CookieDefinitionResponse_Constructor_SetsProperties()
    {
        CookieDefinitionResponse response = new("session", "strictly_necessary", 1, "Session management");

        response.Name.ShouldBe("session");
        response.Category.ShouldBe("strictly_necessary");
        response.RetentionDays.ShouldBe(1);
        response.Purpose.ShouldBe("Session management");
    }

    [Fact]
    public void ThirdPartyServiceResponse_Constructor_SetsProperties()
    {
        List<string> patterns = ["^_pk_", "^mtm_"];
        ThirdPartyServiceResponse response = new("matomo", "analytics", patterns);

        response.Name.ShouldBe("matomo");
        response.Category.ShouldBe("analytics");
        response.CookiePatterns.ShouldBe(patterns);
    }
}
