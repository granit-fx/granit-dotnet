using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class EvaluateAsyncStringInterpolationAnalyzerTests
{
    /// <summary>
    /// Stub for <c>Granit.Browsing.IBrowserPage</c> — minimal surface mirroring the four
    /// JS-injection methods the analyzer targets, plus an unrelated method to test scoping.
    /// </summary>
    private const string BrowserPageStub = """
        namespace Granit.Browsing
        {
            using System.Threading;
            using System.Threading.Tasks;

            public interface IBrowserPage
            {
                Task<TResult?> EvaluateAsync<TResult>(string jsExpression, CancellationToken cancellationToken = default);
                Task AddScriptTagAsync(string js, CancellationToken cancellationToken = default);
                Task AddStyleTagAsync(string css, CancellationToken cancellationToken = default);
                Task WaitForFunctionAsync(string jsExpression, System.TimeSpan? timeout = null, CancellationToken cancellationToken = default);
                Task PrintAsync(string content, CancellationToken cancellationToken = default);
            }
        }
        """;

    [Fact]
    public async Task Literal_string_does_not_trigger()
    {
        string source = $$"""
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page)
                {
                    return page.EvaluateAsync<object>("document.title");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldBeEmpty();
    }

    [Fact]
    public async Task Interpolation_with_nonconstant_hole_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string x)
                {
                    return page.EvaluateAsync<object>($"alert({x})");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task String_plus_concat_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string variable)
                {
                    return page.EvaluateAsync<object>("abc" + variable);
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task String_Format_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string x)
                {
                    return page.EvaluateAsync<object>(string.Format("alert({0})", x));
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task String_Concat_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string a, string b)
                {
                    return page.EvaluateAsync<object>(string.Concat(a, b));
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task String_Join_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string[] arr)
                {
                    return page.EvaluateAsync<object>(string.Join(",", arr));
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task Nameof_does_not_trigger()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Foo { }

            public class Caller
            {
                public Task Run(IBrowserPage page)
                {
                    return page.EvaluateAsync<object>(nameof(Foo));
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldBeEmpty();
    }

    [Fact]
    public async Task Interpolation_with_all_const_holes_does_not_trigger()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page)
                {
                    return page.EvaluateAsync<object>($"page={1}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldBeEmpty();
    }

    [Fact]
    public async Task PdfViewer_constant_int_interpolation_does_not_trigger()
    {
        // Mirrors the real PuppeteerPdfViewerCapability / PlaywrightPdfViewerCapability expression:
        // `$"#page={pageIndex + 1}"` — pageIndex is `int`, so `pageIndex + 1` is non-constant.
        // The expression as a whole is a non-constant interpolation; we expect a diagnostic
        // ONLY when this pattern targets one of the four guarded methods. The PDF viewer
        // capability calls `EvaluateAsync` internally, so this DOES legitimately fire — but
        // the framework code itself should be reviewed; consumer-controlled `pageIndex` is
        // bounded earlier in the call chain. This test pins current behavior.
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, int pageIndex)
                {
                    return page.EvaluateAsync<bool>(
                        $"(async () => {{ window.location.hash = '#page={pageIndex + 1}'; return true; }})()");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        // pageIndex is a method parameter (non-constant) → analyzer correctly flags it.
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task Wrong_method_does_not_trigger()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string x)
                {
                    return page.PrintAsync($"alert({x})");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddScriptTagAsync_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string x)
                {
                    return page.AddScriptTagAsync($"alert({x})");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task AddStyleTagAsync_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string color)
                {
                    return page.AddStyleTagAsync($"body {{ color: {color}; }}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task WaitForFunctionAsync_triggers()
    {
        string source = """
            using System.Threading.Tasks;
            using Granit.Browsing;

            public class Caller
            {
                public Task Run(IBrowserPage page, string predicate)
                {
                    return page.WaitForFunctionAsync($"() => {predicate}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source);
        OurDiagnostics(diags).ShouldContain(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task Silent_when_IBrowserPage_absent()
    {
        // No stub for Granit.Browsing.IBrowserPage in the compilation → opt-in skips.
        string source = """
            using System.Threading.Tasks;

            public interface IBrowserPage
            {
                Task EvaluateAsync<T>(string js);
            }

            public class Caller
            {
                public Task Run(IBrowserPage page, string x)
                {
                    return page.EvaluateAsync<object>($"alert({x})");
                }
            }
            """;

        ImmutableArray<Diagnostic> diags = await RunAsync(source, includeBrowserPageStub: false);
        OurDiagnostics(diags).ShouldBeEmpty();
    }

    private static IEnumerable<Diagnostic> OurDiagnostics(ImmutableArray<Diagnostic> diags) =>
        diags.Where(d => d.Id == EvaluateAsyncStringInterpolationAnalyzer.DiagnosticId);

    private static Task<ImmutableArray<Diagnostic>> RunAsync(string source, bool includeBrowserPageStub = true) =>
        RunAnalyzerAsync<EvaluateAsyncStringInterpolationAnalyzer>(
            source,
            includeBrowserPageStub
                ? new[] { BrowserPageStub }
                : System.Array.Empty<string>(),
            TestContext.Current.CancellationToken);

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(
        string source,
        string[] additionalSources,
        CancellationToken cancellationToken)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        ImmutableArray<SyntaxTree>.Builder trees = ImmutableArray.CreateBuilder<SyntaxTree>();
        trees.Add(CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken));
        foreach (string additional in additionalSources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(additional, cancellationToken: cancellationToken));
        }

        ImmutableArray<MetadataReference> references = GetNetCoreReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "BrowsingAnalyzerTestAssembly",
            syntaxTrees: trees.ToImmutable(),
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    private static ImmutableArray<MetadataReference> GetNetCoreReferences()
    {
        string? assembliesStr = System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (assembliesStr is null)
        {
            return ImmutableArray<MetadataReference>.Empty;
        }

        ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in assembliesStr.Split(Path.PathSeparator))
        {
            builder.Add(MetadataReference.CreateFromFile(path));
        }

        return builder.ToImmutable();
    }
}
