using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

public sealed class CookieConsentConfigProviderTests
{
    private readonly ICookieRegistry _cookieRegistry = Substitute.For<ICookieRegistry>();
    private readonly IThirdPartyServiceRegistry _serviceRegistry = Substitute.For<IThirdPartyServiceRegistry>();

    [Fact]
    public void GetConfig_WithEmptyRegistries_ReturnsEmptyResponse()
    {
        // Arrange
        _cookieRegistry.GetAll().Returns([]);
        _serviceRegistry.GetAll().Returns([]);
        CookieConsentConfigProvider provider = new(_cookieRegistry, _serviceRegistry);

        // Act
        CookieConsentConfigResponse result = provider.GetConfig();

        // Assert
        result.Cookies.ShouldBeEmpty();
        result.Services.ShouldBeEmpty();
    }

    [Fact]
    public void GetConfig_WithCookies_MapsCookieDefinitionsToResponse()
    {
        // Arrange
        IReadOnlyList<CookieDefinition> cookies =
        [
            new("session_id", CookieCategory.StrictlyNecessary, 0, true, "Session tracking"),
            new("lang", CookieCategory.Preferences, 365, false, "Language preference"),
        ];
        _cookieRegistry.GetAll().Returns(cookies);
        _serviceRegistry.GetAll().Returns([]);
        CookieConsentConfigProvider provider = new(_cookieRegistry, _serviceRegistry);

        // Act
        CookieConsentConfigResponse result = provider.GetConfig();

        // Assert
        result.Cookies.Count.ShouldBe(2);

        result.Cookies[0].Name.ShouldBe("session_id");
        result.Cookies[0].Category.ShouldBe("strictly_necessary");
        result.Cookies[0].RetentionDays.ShouldBe(0);
        result.Cookies[0].Purpose.ShouldBe("Session tracking");

        result.Cookies[1].Name.ShouldBe("lang");
        result.Cookies[1].Category.ShouldBe("preferences");
        result.Cookies[1].RetentionDays.ShouldBe(365);
        result.Cookies[1].Purpose.ShouldBe("Language preference");
    }

    [Fact]
    public void GetConfig_WithServices_MapsServiceDefinitionsToResponse()
    {
        // Arrange
        IReadOnlyList<ThirdPartyServiceDefinition> services =
        [
            new("matomo", CookieCategory.Analytics, ["^_pk_"]),
            new("hubspot", CookieCategory.Marketing, ["^__hs", "^hubspot"]),
        ];
        _cookieRegistry.GetAll().Returns([]);
        _serviceRegistry.GetAll().Returns(services);
        CookieConsentConfigProvider provider = new(_cookieRegistry, _serviceRegistry);

        // Act
        CookieConsentConfigResponse result = provider.GetConfig();

        // Assert
        result.Services.Count.ShouldBe(2);

        result.Services[0].Name.ShouldBe("matomo");
        result.Services[0].Category.ShouldBe("analytics");
        result.Services[0].CookiePatterns.ShouldBe(["^_pk_"]);

        result.Services[1].Name.ShouldBe("hubspot");
        result.Services[1].Category.ShouldBe("marketing");
        result.Services[1].CookiePatterns.ShouldBe(["^__hs", "^hubspot"]);
    }

    [Theory]
    [InlineData(CookieCategory.StrictlyNecessary, "strictly_necessary")]
    [InlineData(CookieCategory.Preferences, "preferences")]
    [InlineData(CookieCategory.Analytics, "analytics")]
    [InlineData(CookieCategory.Marketing, "marketing")]
    public void GetConfig_ConvertsAllCategoriesToSnakeCase(CookieCategory category, string expected)
    {
        // Arrange
        _cookieRegistry.GetAll().Returns<IReadOnlyList<CookieDefinition>>(
            [new("test", category, 30, false, "Test cookie")]);
        _serviceRegistry.GetAll().Returns([]);
        CookieConsentConfigProvider provider = new(_cookieRegistry, _serviceRegistry);

        // Act
        CookieConsentConfigResponse result = provider.GetConfig();

        // Assert
        result.Cookies[0].Category.ShouldBe(expected);
    }
}
