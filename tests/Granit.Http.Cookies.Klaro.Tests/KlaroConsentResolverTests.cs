using System.Text.Json;
using Granit.Http.Cookies.Klaro.Internal;
using Granit.Http.Cookies.Klaro.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Klaro.Tests;

public sealed class KlaroConsentResolverTests
{
    private readonly KlaroOptions _options = new() { CookieName = "klaro" };

    private readonly IThirdPartyServiceRegistry _serviceRegistry = CreateDefaultRegistry();

    private static IThirdPartyServiceRegistry CreateDefaultRegistry()
    {
        List<ThirdPartyServiceDefinition> services =
        [
            new("google-analytics", CookieCategory.Analytics, []),
            new("matomo", CookieCategory.Analytics, []),
            new("youtube", CookieCategory.Marketing, []),
            new("theme-preference", CookieCategory.Preferences, []),
        ];

        IThirdPartyServiceRegistry registry = Substitute.For<IThirdPartyServiceRegistry>();
        registry.GetAll().Returns(services);
        registry.GetByCategory(Arg.Any<CookieCategory>())
            .Returns(callInfo =>
            {
                CookieCategory category = callInfo.Arg<CookieCategory>();
                return services.Where(s => s.Category == category).ToList();
            });

        return registry;
    }

    private static IThirdPartyServiceRegistry CreateRegistry(
        params ThirdPartyServiceDefinition[] services)
    {
        List<ThirdPartyServiceDefinition> list = [.. services];
        IThirdPartyServiceRegistry registry = Substitute.For<IThirdPartyServiceRegistry>();
        registry.GetAll().Returns(list);
        registry.GetByCategory(Arg.Any<CookieCategory>())
            .Returns(callInfo =>
            {
                CookieCategory category = callInfo.Arg<CookieCategory>();
                return list.Where(s => s.Category == category).ToList();
            });

        return registry;
    }

    private KlaroConsentResolver CreateResolver(
        KlaroOptions? options = null,
        IThirdPartyServiceRegistry? registry = null)
    {
        KlaroOptions opts = options ?? _options;
        IOptions<KlaroOptions> wrappedOptions = Microsoft.Extensions.Options.Options.Create(opts);
        ILogger<KlaroConsentResolver> logger = NullLogger<KlaroConsentResolver>.Instance;
        return new KlaroConsentResolver(wrappedOptions, registry ?? _serviceRegistry, logger);
    }

    private static DefaultHttpContext CreateHttpContext(string cookieName, string cookieValue)
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Cookie = $"{cookieName}={Uri.EscapeDataString(cookieValue)}";
        return context;
    }

    private static DefaultHttpContext CreateHttpContextNoCookie() => new();

    [Fact]
    public async Task StrictlyNecessary_AlwaysTrue()
    {
        KlaroConsentResolver resolver = CreateResolver();
        DefaultHttpContext httpContext = CreateHttpContextNoCookie();

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.StrictlyNecessary);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingCookie_ReturnsFalse()
    {
        KlaroConsentResolver resolver = CreateResolver();
        DefaultHttpContext httpContext = CreateHttpContextNoCookie();

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task EmptyCookie_ReturnsFalse()
    {
        KlaroConsentResolver resolver = CreateResolver();
        DefaultHttpContext httpContext = CreateHttpContext("klaro", "");

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AllServicesConsented_ReturnsTrue()
    {
        KlaroConsentResolver resolver = CreateResolver();
        string json = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["google-analytics"] = true,
            ["matomo"] = true,
        });
        DefaultHttpContext httpContext = CreateHttpContext("klaro", json);

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task OneServiceNotConsented_ReturnsFalse()
    {
        KlaroConsentResolver resolver = CreateResolver();
        string json = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["google-analytics"] = true,
            ["matomo"] = false,
        });
        DefaultHttpContext httpContext = CreateHttpContext("klaro", json);

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ServiceMissing_ReturnsFalse()
    {
        KlaroConsentResolver resolver = CreateResolver();
        string json = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["google-analytics"] = true,
            // matomo is missing from the cookie
        });
        DefaultHttpContext httpContext = CreateHttpContext("klaro", json);

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task NoMappingsForCategory_ReturnsFalse()
    {
        IThirdPartyServiceRegistry registry = CreateRegistry(
            new ThirdPartyServiceDefinition("google-analytics", CookieCategory.Analytics, []));

        KlaroConsentResolver resolver = CreateResolver(registry: registry);
        string json = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["google-analytics"] = true,
        });
        DefaultHttpContext httpContext = CreateHttpContext("klaro", json);

        // Marketing has no mappings
        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Marketing);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidJson_ReturnsFalse()
    {
        KlaroConsentResolver resolver = CreateResolver();
        DefaultHttpContext httpContext = CreateHttpContext("klaro", "not-valid-json{{{");

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CustomCookieName_ReadsCookie()
    {
        IThirdPartyServiceRegistry registry = CreateRegistry(
            new ThirdPartyServiceDefinition("youtube", CookieCategory.Marketing, []));

        KlaroOptions options = new() { CookieName = "my-consent" };
        KlaroConsentResolver resolver = CreateResolver(options, registry);
        string json = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["youtube"] = true,
        });
        DefaultHttpContext httpContext = CreateHttpContext("my-consent", json);

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Marketing);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CaseSensitiveServiceNames()
    {
        KlaroConsentResolver resolver = CreateResolver();
        // JSON keys are case-sensitive: "Google-Analytics" ≠ "google-analytics"
        string json = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["Google-Analytics"] = true,
            ["Matomo"] = true,
        });
        DefaultHttpContext httpContext = CreateHttpContext("klaro", json);

        bool result = await resolver.ResolveAsync(httpContext, CookieCategory.Analytics);

        // Should return false because JSON property names are case-sensitive
        result.ShouldBeFalse();
    }
}
