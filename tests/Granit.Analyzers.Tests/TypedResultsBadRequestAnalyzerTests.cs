using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class TypedResultsBadRequestAnalyzerTests
{
    private static readonly string[] ApiStubs = [AnalyzerTestHelpers.MinimalApiResultsStub];

    [Fact]
    public async Task GRAPI002_fires_on_BadRequest_with_string_arg()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public BadRequest<string> Handle() => TypedResults.BadRequest("error message");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI002_fires_on_BadRequest_with_object_arg()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public BadRequest<object> Handle() => TypedResults.BadRequest<object>(new { error = "msg" });
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRAPI002_silent_on_BadRequest_no_args()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public BadRequest Handle() => TypedResults.BadRequest();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI002_silent_on_TypedResults_Problem()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public ProblemHttpResult Handle() =>
                    TypedResults.Problem(detail: "error", statusCode: StatusCodes.Status400BadRequest);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI002_silent_on_TypedResults_Ok()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public Ok<string> Handle() => TypedResults.Ok("hello");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI002_silent_on_custom_BadRequest_method()
    {
        string source = """
            public static class TypedResults
            {
                public static string BadRequest(string msg) => msg;
            }

            public class MyService
            {
                public string Handle() => TypedResults.BadRequest("error");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRAPI002_reports_multiple_occurrences()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            public class MyEndpoint
            {
                public BadRequest<string> First() => TypedResults.BadRequest("error 1");
                public BadRequest<string> Second() => TypedResults.BadRequest("error 2");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TypedResultsBadRequestAnalyzer>(
                source, ApiStubs, TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TypedResultsBadRequestAnalyzer.DiagnosticId)
            .Count().ShouldBe(2);
    }
}
