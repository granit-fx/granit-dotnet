using Granit.Http.Cookies.Exceptions;
using Granit.Http.Cookies.Internal;
using Granit.Timing;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class GranitCookieManagerTests
{
    private readonly CookieRegistry _registry = new();
    private readonly IConsentResolver _consentResolver = Substitute.For<IConsentResolver>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly GranitCookieManager _sut;

    public GranitCookieManagerTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        _sut = new GranitCookieManager(_registry, _consentResolver, _clock);
    }

    private static DefaultHttpContext CreateHttpContext() => new();

    [Fact]
    public async Task SetCookieAsync_RegisteredWithConsent_WritesCookie()
    {
        CookieDefinition definition = new("analytics_id", CookieCategory.Analytics, 365, false, "Analytics");
        _registry.Register(definition);
        DefaultHttpContext httpContext = CreateHttpContext();
        _consentResolver.ResolveAsync(httpContext, CookieCategory.Analytics).Returns(true);

        await _sut.SetCookieAsync(httpContext, "analytics_id", "abc123");

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldContain("analytics_id=abc123");
    }

    [Fact]
    public async Task SetCookieAsync_StrictlyNecessary_BypassesConsent()
    {
        CookieDefinition definition = new("session_id", CookieCategory.StrictlyNecessary, 1, true, "Session");
        _registry.Register(definition);
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.SetCookieAsync(httpContext, "session_id", "sess_xyz");

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldContain("session_id=sess_xyz");
        await _consentResolver.DidNotReceive().ResolveAsync(Arg.Any<HttpContext>(), Arg.Any<CookieCategory>());
    }

    [Fact]
    public async Task SetCookieAsync_NoConsent_SkipsSilently()
    {
        CookieDefinition definition = new("marketing_id", CookieCategory.Marketing, 365, false, "Marketing");
        _registry.Register(definition);
        DefaultHttpContext httpContext = CreateHttpContext();
        _consentResolver.ResolveAsync(httpContext, CookieCategory.Marketing).Returns(false);

        await _sut.SetCookieAsync(httpContext, "marketing_id", "mkt_123");

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task SetCookieAsync_Unregistered_ThrowsUnregisteredCookieException()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        Func<Task> act = () => _sut.SetCookieAsync(httpContext, "unknown_cookie", "value");

        UnregisteredCookieException ex = await Should.ThrowAsync<UnregisteredCookieException>(act);
        ex.CookieName.ShouldBe("unknown_cookie");
    }

    [Fact]
    public async Task SetCookieAsync_UsesClockForExpiration()
    {
        CookieDefinition definition = new("pref_cookie", CookieCategory.Preferences, 30, true, "Preferences");
        _registry.Register(definition);
        DefaultHttpContext httpContext = CreateHttpContext();
        _consentResolver.ResolveAsync(httpContext, CookieCategory.Preferences).Returns(true);

        await _sut.SetCookieAsync(httpContext, "pref_cookie", "value");

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldContain("pref_cookie=value");
        setCookieHeader.ShouldContain("expires=");
        setCookieHeader.ShouldContain("secure");
        setCookieHeader.ShouldContain("httponly");
    }

    [Fact]
    public async Task SetCookieAsync_SetsSecureAndSameSiteLax()
    {
        CookieDefinition definition = new("session_id", CookieCategory.StrictlyNecessary, 1, true, "Session");
        _registry.Register(definition);
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.SetCookieAsync(httpContext, "session_id", "val");

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldContain("secure");
        setCookieHeader.ShouldContain("samesite=lax");
    }

    [Fact]
    public async Task RevokeCategoryAsync_DeletesAllCookiesInCategory()
    {
        _registry.Register(new("analytics_1", CookieCategory.Analytics, 365, false, "Analytics 1"));
        _registry.Register(new("analytics_2", CookieCategory.Analytics, 365, false, "Analytics 2"));
        _registry.Register(new("session", CookieCategory.StrictlyNecessary, 1, true, "Session"));
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.RevokeCategoryAsync(httpContext, CookieCategory.Analytics);

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldContain("analytics_1");
        setCookieHeader.ShouldContain("analytics_2");
        setCookieHeader.ShouldNotContain("session");
    }

    [Fact]
    public void DeleteCookie_RegisteredCookie_DeletesCookie()
    {
        _registry.Register(new("my_cookie", CookieCategory.Preferences, 180, true, "Prefs"));
        DefaultHttpContext httpContext = CreateHttpContext();

        _sut.DeleteCookie(httpContext, "my_cookie");

        string? setCookieHeader = httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.ShouldContain("my_cookie");
    }

    [Fact]
    public void DeleteCookie_Unregistered_ThrowsUnregisteredCookieException()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        Action act = () => _sut.DeleteCookie(httpContext, "unknown_cookie");

        UnregisteredCookieException ex = Should.Throw<UnregisteredCookieException>(act);
        ex.CookieName.ShouldBe("unknown_cookie");
    }
}
