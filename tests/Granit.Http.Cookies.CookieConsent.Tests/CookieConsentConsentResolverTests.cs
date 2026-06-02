using System.Text.Json;
using Granit.Http.Cookies.CookieConsent.Internal;
using Granit.Http.Cookies.CookieConsent.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.CookieConsent.Tests;

public sealed class CookieConsentConsentResolverTests
{
    private readonly CookieConsentOptions _options = new();

    private CookieConsentConsentResolver CreateResolver(CookieConsentOptions? options = null)
    {
        IOptions<CookieConsentOptions> wrappedOptions =
            Microsoft.Extensions.Options.Options.Create(options ?? _options);
        return new CookieConsentConsentResolver(
            wrappedOptions,
            NullLogger<CookieConsentConsentResolver>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext(string cookieName, string cookieValue)
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Cookie = $"{cookieName}={Uri.EscapeDataString(cookieValue)}";
        return context;
    }

    private static DefaultHttpContext CreateHttpContextNoCookie() => new();

    private static string BuildCookie(params string[] categories)
    {
        var payload = new { categories };
        return JsonSerializer.Serialize(payload);
    }

    // ── StrictlyNecessary ────────────────────────────────────────────────────

    [Fact]
    public async Task StrictlyNecessary_AlwaysTrue_NoCookie()
    {
        CookieConsentConsentResolver resolver = CreateResolver();

        bool result = await resolver.HasConsentAsync(
            CreateHttpContextNoCookie(), CookieCategory.StrictlyNecessary);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task StrictlyNecessary_AlwaysTrue_EmptyCookie()
    {
        CookieConsentConsentResolver resolver = CreateResolver();

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", ""), CookieCategory.StrictlyNecessary);

        result.ShouldBeTrue();
    }

    // ── Missing / malformed cookie ────────────────────────────────────────────

    [Fact]
    public async Task MissingCookie_ReturnsFalse()
    {
        CookieConsentConsentResolver resolver = CreateResolver();

        bool result = await resolver.HasConsentAsync(
            CreateHttpContextNoCookie(), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task EmptyCookieValue_ReturnsFalse()
    {
        CookieConsentConsentResolver resolver = CreateResolver();

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", ""), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidJson_ReturnsFalse()
    {
        CookieConsentConsentResolver resolver = CreateResolver();

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", "not-valid-json{{{"), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingCategoriesProperty_ReturnsFalse()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        string cookie = JsonSerializer.Serialize(new { revision = 0 });

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    // ── Consent granted / denied ──────────────────────────────────────────────

    [Fact]
    public async Task AnalyticsGranted_ReturnsTrue()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        string cookie = BuildCookie("necessary", "analytics");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyticsAbsentFromArray_ReturnsFalse()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        string cookie = BuildCookie("necessary", "marketing");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task MarketingGranted_ReturnsTrue()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        string cookie = BuildCookie("necessary", "functional", "analytics", "marketing");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Marketing);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task PreferencesGranted_ReturnsTrue()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        // Preferences maps to "functional" by default
        string cookie = BuildCookie("necessary", "functional");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Preferences);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task SaleOrSharingGranted_ReturnsTrue()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        string cookie = BuildCookie("necessary", "sale_or_sharing");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.SaleOrSharing);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyCategoriesArray_ReturnsFalse()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        string cookie = BuildCookie();

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    // ── Category name matching ────────────────────────────────────────────────

    [Fact]
    public async Task CategoryNameIsCaseSensitive()
    {
        CookieConsentConsentResolver resolver = CreateResolver();
        // "Analytics" (wrong case) should not match "analytics"
        string cookie = BuildCookie("necessary", "Analytics");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    // ── Custom options ────────────────────────────────────────────────────────

    [Fact]
    public async Task CustomCookieName_ReadsCookie()
    {
        CookieConsentOptions options = new() { CookieName = "my-consent" };
        CookieConsentConsentResolver resolver = CreateResolver(options);
        string cookie = BuildCookie("necessary", "analytics");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("my-consent", cookie), CookieCategory.Analytics);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CustomCookieName_WrongCookieRead_ReturnsFalse()
    {
        CookieConsentOptions options = new() { CookieName = "my-consent" };
        CookieConsentConsentResolver resolver = CreateResolver(options);
        string cookie = BuildCookie("necessary", "analytics");

        // Stored under the default name, not the custom one
        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CustomAnalyticsCategoryName_IsRespected()
    {
        CookieConsentOptions options = new() { AnalyticsCategoryName = "stats" };
        CookieConsentConsentResolver resolver = CreateResolver(options);
        string cookie = BuildCookie("necessary", "stats");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Analytics);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CustomFunctionalCategoryName_IsRespected()
    {
        CookieConsentOptions options = new() { FunctionalCategoryName = "preferences" };
        CookieConsentConsentResolver resolver = CreateResolver(options);
        string cookie = BuildCookie("necessary", "preferences");

        bool result = await resolver.HasConsentAsync(
            CreateHttpContext("cc_cookie", cookie), CookieCategory.Preferences);

        result.ShouldBeTrue();
    }
}
