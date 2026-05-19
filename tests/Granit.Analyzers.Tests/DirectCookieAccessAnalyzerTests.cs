using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class DirectCookieAccessAnalyzerTests
{
    private static readonly string[] CookieStubs =
    [
        AnalyzerTestHelpers.ResponseCookiesStub,
        AnalyzerTestHelpers.GranitCookieManagerStub
    ];

    [Fact]
    public async Task GRSEC004_fires_on_Cookies_Append()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyController
            {
                public void SetCookie(HttpContext context)
                {
                    context.Response.Cookies.Append("key", "value");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DirectCookieAccessAnalyzer>(
                source,
                CookieStubs,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DirectCookieAccessAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC004_fires_on_Cookies_Delete()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyController
            {
                public void RemoveCookie(HttpContext context)
                {
                    context.Response.Cookies.Delete("key");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DirectCookieAccessAnalyzer>(
                source,
                CookieStubs,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DirectCookieAccessAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC004_silent_when_Granit_Cookies_absent()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyController
            {
                public void SetCookie(HttpContext context)
                {
                    context.Response.Cookies.Append("key", "value");
                }
            }
            """;

        // Only include ResponseCookies stub, NOT GranitCookieManager → opt-in should skip
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DirectCookieAccessAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.ResponseCookiesStub },
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DirectCookieAccessAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC004_silent_on_custom_Append_method()
    {
        string source = """
            public class MyList
            {
                public void Append(string item) { }
            }

            public class Service
            {
                public void AddItem(MyList list)
                {
                    list.Append("item");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DirectCookieAccessAnalyzer>(
                source,
                CookieStubs,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DirectCookieAccessAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC004_fires_multiple_occurrences()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyController
            {
                public void ManageCookies(HttpContext context)
                {
                    context.Response.Cookies.Append("key1", "value1");
                    context.Response.Cookies.Append("key2", "value2");
                    context.Response.Cookies.Delete("key3");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DirectCookieAccessAnalyzer>(
                source,
                CookieStubs,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DirectCookieAccessAnalyzer.DiagnosticId)
            .Count().ShouldBe(3);
    }

    [Fact]
    public async Task GRSEC004_fires_on_direct_IResponseCookies_variable()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyService
            {
                public void WriteCookie(IResponseCookies cookies)
                {
                    cookies.Append("key", "value");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DirectCookieAccessAnalyzer>(
                source,
                CookieStubs,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DirectCookieAccessAnalyzer.DiagnosticId);
    }
}
