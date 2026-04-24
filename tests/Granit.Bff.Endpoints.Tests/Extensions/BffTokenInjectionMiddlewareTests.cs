using Granit.Bff.Endpoints.Extensions;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Shouldly;
using Xunit;

namespace Granit.Bff.Endpoints.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="BffTokenInjectionMiddleware.ResolveFrontendFromCookies"/>.
/// Exercises the multi-frontend cookie disambiguation logic added to fix the 403
/// that occurred when a user held a live session on two BFFs served from the same
/// backend origin.
/// </summary>
public sealed class BffTokenInjectionMiddlewareTests
{
    private static BffFrontendOptions Frontend(string name, string? clientUrl, string cookiePrefix = ".") =>
        new()
        {
            Name = name,
            ClientId = $"client-{name}",
            ClientSecret = "secret",
            ClientUrl = clientUrl,
            CookiePrefix = cookiePrefix,
        };

    private static DefaultHttpContext CreateContext(
        Dictionary<string, string> cookies,
        string? origin = null,
        string? referer = null)
    {
        DefaultHttpContext context = new();

        if (cookies.Count > 0)
        {
            string header = string.Join(
                "; ",
                cookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            context.Request.Headers.Cookie = new StringValues(header);
        }

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (referer is not null)
        {
            context.Request.Headers.Referer = referer;
        }

        return context;
    }

    [Fact]
    public void ResolveFrontendFromCookies_NoCookies_ReturnsNull()
    {
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [Frontend("host", "http://localhost:5173")],
        };

        HttpContext context = CreateContext(new Dictionary<string, string>());

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBeNull();
        sessionId.ShouldBeNull();
    }

    [Fact]
    public void ResolveFrontendFromCookies_SingleCookie_ReturnsMatchingFrontend()
    {
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(new Dictionary<string, string>
        {
            [".bff-app"] = "session-app-123",
        });

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(app);
        sessionId.ShouldBe("session-app-123");
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_WithOrigin_ReturnsCallerFrontend()
    {
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        // Simulate the bug scenario: user is authenticated on both BFFs, SPA at
        // localhost:5174 (app) makes a call — without disambiguation the middleware
        // would pick "host" (first in options) and the HMAC-bound CSRF check fails.
        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
                [".bff-app"] = "session-app-bbb",
            },
            origin: "http://localhost:5174");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(app);
        sessionId.ShouldBe("session-app-bbb");
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_WithOriginMatchingHost_ReturnsHost()
    {
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
                [".bff-app"] = "session-app-bbb",
            },
            origin: "http://localhost:5173");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(host);
        sessionId.ShouldBe("session-host-aaa");
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_NoOriginButReferer_ReturnsRefererFrontend()
    {
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
                [".bff-app"] = "session-app-bbb",
            },
            referer: "http://localhost:5174/some/page");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(app);
        sessionId.ShouldBe("session-app-bbb");
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_NullOriginHeader_FallsBackToReferer()
    {
        // Some browsers send "Origin: null" for sandboxed/privacy contexts — treat as missing.
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
                [".bff-app"] = "session-app-bbb",
            },
            origin: "null",
            referer: "http://localhost:5174/");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(app);
        sessionId.ShouldBe("session-app-bbb");
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_UnknownOrigin_ReturnsNull()
    {
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
                [".bff-app"] = "session-app-bbb",
            },
            origin: "http://unrelated.example:8080");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        // Caller origin does not match any frontend's ClientUrl — granting one
        // arbitrary session to a stranger would hand it CSRF-protected state it
        // has no claim to. Drop both candidates instead.
        frontend.ShouldBeNull();
        sessionId.ShouldBeNull();
    }

    [Fact]
    public void ResolveFrontendFromCookies_SingleCookie_OriginMismatch_ReturnsNull()
    {
        // Regression: on localhost the cookie scope ignores the port (RFC 6265),
        // so a `.bff-host` cookie set by the host SPA on :5173 is also sent by
        // the browser to :5174. With only one cookie the old fast path returned
        // it unconditionally — the tenant SPA's mutating request (e.g. login)
        // was then CSRF-validated against the host session's HMAC secret and
        // rejected with 403.
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
            },
            origin: "http://localhost:5174");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBeNull();
        sessionId.ShouldBeNull();
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_NoOriginOrReferer_FallsBackToFirst()
    {
        BffFrontendOptions host = Frontend("host", "http://localhost:5173");
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(new Dictionary<string, string>
        {
            [".bff-host"] = "session-host-aaa",
            [".bff-app"] = "session-app-bbb",
        });

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(host);
        sessionId.ShouldBe("session-host-aaa");
    }

    [Fact]
    public void ResolveFrontendFromCookies_OriginMatchIsCaseInsensitiveOnHost()
    {
        BffFrontendOptions host = Frontend("host", "https://Admin.Example.com", cookiePrefix: "__Host-");
        BffFrontendOptions app = Frontend("app", "https://App.Example.com", cookiePrefix: "__Host-");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        DefaultHttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                ["__Host-bff-host"] = "session-host-aaa",
                ["__Host-bff-app"] = "session-app-bbb",
            },
            origin: "https://app.example.com");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(app);
        sessionId.ShouldBe("session-app-bbb");
    }

    [Fact]
    public void ResolveFrontendFromCookies_BothCookiesPresent_CandidateWithoutClientUrl_IsSkipped()
    {
        // A frontend without ClientUrl cannot be matched by Origin — it must lose
        // the arbitration to frontends that have a ClientUrl when a caller origin exists.
        BffFrontendOptions host = Frontend("host", clientUrl: null);
        BffFrontendOptions app = Frontend("app", "http://localhost:5174");
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [host, app],
        };

        HttpContext context = CreateContext(
            new Dictionary<string, string>
            {
                [".bff-host"] = "session-host-aaa",
                [".bff-app"] = "session-app-bbb",
            },
            origin: "http://localhost:5174");

        (BffFrontendOptions? frontend, string? sessionId) =
            BffTokenInjectionMiddleware.ResolveFrontendFromCookies(context, options);

        frontend.ShouldBe(app);
        sessionId.ShouldBe("session-app-bbb");
    }
}
