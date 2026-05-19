using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class MinimalApiServiceParameterAnalyzerTests
{
    private static readonly string[] Stubs =
    [
        AnalyzerTestHelpers.MinimalApiResultsStub,
        AnalyzerTestHelpers.MinimalApiBindingStub,
    ];

    // ── Should fire ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GRAPI003_fires_on_interface_param_without_FromServices()
    {
        string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public interface IMyService { }

            public static class MyEndpoints
            {
                private static async Task<Ok<string>> GetAsync(
                    string id,
                    IMyService service,
                    CancellationToken ct)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI003_fires_on_multiple_unbound_interface_params()
    {
        string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public interface IReader { }
            public interface IWriter { }

            public static class MyEndpoints
            {
                private static async Task<Ok<string>> PostAsync(
                    IReader reader,
                    IWriter writer,
                    CancellationToken ct)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .Count().ShouldBe(2);
    }

    // ── Should NOT fire ──────────────────────────────────────────────────────

    [Fact]
    public async Task GRAPI003_silent_on_FromServices_attribute()
    {
        string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Mvc;

            public interface IMyService { }

            public static class MyEndpoints
            {
                private static async Task<Ok<string>> GetAsync(
                    string id,
                    [FromServices] IMyService service,
                    CancellationToken ct)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI003_silent_on_non_static_method()
    {
        string source = """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http.HttpResults;

            public interface IMyService { }

            public class MyEndpoints
            {
                private async Task<Ok<string>> GetAsync(IMyService service)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI003_silent_on_non_IResult_return_type()
    {
        string source = """
            public interface IMyService { }

            public static class MyService
            {
                private static string Process(IMyService service) => "ok";
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI003_silent_on_CancellationToken_not_interface()
    {
        string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http.HttpResults;

            public static class MyEndpoints
            {
                private static async Task<Ok<string>> GetAsync(CancellationToken ct)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI003_silent_on_AsParameters_attribute()
    {
        string source = """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Http;

            public interface IMyQuery { }

            public static class MyEndpoints
            {
                private static async Task<Ok<string>> GetAsync(
                    [AsParameters] IMyQuery query)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI003_silent_on_FromQuery_attribute()
    {
        string source = """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Mvc;

            public interface IMyFilter { }

            public static class MyEndpoints
            {
                private static async Task<Ok<string>> GetAsync(
                    [FromQuery] IMyFilter filter)
                    => TypedResults.Ok("ok");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<MinimalApiServiceParameterAnalyzer>(
                source, Stubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == MinimalApiServiceParameterAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
