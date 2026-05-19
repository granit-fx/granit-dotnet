using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class UntypedResultsAnalyzerTests
{
    private static readonly string[] ApiStubs = [AnalyzerTestHelpers.MinimalApiResultsStub];

    [Fact]
    public async Task GRAPI001_fires_on_Results_Ok()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyEndpoint
            {
                public IResult Handle() => Results.Ok();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == UntypedResultsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI001_fires_on_Results_BadRequest()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyEndpoint
            {
                public IResult Handle() => Results.BadRequest("error");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == UntypedResultsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI001_fires_on_Results_NotFound()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyEndpoint
            {
                public IResult Handle() => Results.NotFound();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == UntypedResultsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI001_fires_on_Results_Problem()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyEndpoint
            {
                public IResult Handle() => Results.Problem(detail: "error", statusCode: 400);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == UntypedResultsAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI001_silent_on_TypedResults_Ok()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public Ok Handle() => TypedResults.Ok();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == UntypedResultsAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI001_silent_on_TypedResults_Problem()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public ProblemHttpResult Handle() => TypedResults.Problem(detail: "error", statusCode: 400);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == UntypedResultsAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI001_silent_on_custom_Results_class()
    {
        string source = """
            public static class Results
            {
                public static string Ok() => "ok";
            }

            public class MyService
            {
                public string Handle() => Results.Ok();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == UntypedResultsAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI001_reports_multiple_occurrences()
    {
        string source = """
            using Microsoft.AspNetCore.Http;

            public class MyEndpoint
            {
                public IResult Success() => Results.Ok();
                public IResult Failure() => Results.BadRequest();
                public IResult Missing() => Results.NotFound();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<UntypedResultsAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == UntypedResultsAnalyzer.DiagnosticId)
            .Count().ShouldBe(3);
    }
}
