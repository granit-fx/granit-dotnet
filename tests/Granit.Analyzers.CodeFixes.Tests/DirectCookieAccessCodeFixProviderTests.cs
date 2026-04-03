using System.Threading.Tasks;
using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class DirectCookieAccessCodeFixProviderTests
{
    private static readonly string[] CookieStubs =
    [
        Analyzers.Tests.AnalyzerTestHelpers.ResponseCookiesStub,
        Analyzers.Tests.AnalyzerTestHelpers.GranitCookieManagerStub,
    ];

    [Fact]
    public async Task Replaces_Append_with_SetCookieAsync_and_injects_IGranitCookieManager()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public class MyMiddleware
            {
                public MyMiddleware() { }
                public void Handle(HttpContext httpContext)
                {
                    httpContext.Response.Cookies.Append("token", "abc");
                }
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Granit.Http.Cookies;

            public class MyMiddleware
            {
                private readonly IGranitCookieManager _cookieManager;

                public MyMiddleware(IGranitCookieManager cookieManager)
                {
                    _cookieManager = cookieManager;
                }
                public void Handle(HttpContext httpContext)
                {
                    await _cookieManager.SetCookieAsync(httpContext, "token", "abc");
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DirectCookieAccessAnalyzer, DirectCookieAccessCodeFixProvider>(
            source, expected, CookieStubs);
    }

    [Fact]
    public async Task Replaces_Delete_with_DeleteCookie_and_injects_IGranitCookieManager()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public class MyMiddleware
            {
                public MyMiddleware() { }
                public void Handle(HttpContext httpContext)
                {
                    httpContext.Response.Cookies.Delete("token");
                }
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Granit.Http.Cookies;

            public class MyMiddleware
            {
                private readonly IGranitCookieManager _cookieManager;

                public MyMiddleware(IGranitCookieManager cookieManager)
                {
                    _cookieManager = cookieManager;
                }
                public void Handle(HttpContext httpContext)
                {
                    _cookieManager.DeleteCookie(httpContext, "token");
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DirectCookieAccessAnalyzer, DirectCookieAccessCodeFixProvider>(
            source, expected, CookieStubs);
    }

    [Fact]
    public async Task Does_not_duplicate_field_when_cookieManager_already_exists()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public class MyMiddleware
            {
                private readonly IGranitCookieManager _cookieManager;
                public MyMiddleware(IGranitCookieManager cookieManager) { _cookieManager = cookieManager; }
                public void Handle(HttpContext httpContext)
                {
                    httpContext.Response.Cookies.Append("token", "abc");
                }
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Granit.Http.Cookies;

            public class MyMiddleware
            {
                private readonly IGranitCookieManager _cookieManager;
                public MyMiddleware(IGranitCookieManager cookieManager) { _cookieManager = cookieManager; }
                public void Handle(HttpContext httpContext)
                {
                    await _cookieManager.SetCookieAsync(httpContext, "token", "abc");
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DirectCookieAccessAnalyzer, DirectCookieAccessCodeFixProvider>(
            source, expected, CookieStubs);
    }
}
