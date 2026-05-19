using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class TypedResultsBadRequestCodeFixProviderTests
{
    private static readonly string[] ApiStubs = [Analyzers.Tests.AnalyzerTestHelpers.MinimalApiResultsStub];

    [Fact]
    public async Task Replaces_BadRequest_string_with_Problem()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            public class MyEndpoint
            {
                public BadRequest<string> Handle() => TypedResults.BadRequest("error message");
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            public class MyEndpoint
            {
                public BadRequest<string> Handle() => TypedResults.Problem(detail: "error message", statusCode: StatusCodes.Status400BadRequest);
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<TypedResultsBadRequestAnalyzer, TypedResultsBadRequestCodeFixProvider>(
            source, expected, ApiStubs);
    }

    [Fact]
    public async Task Replaces_BadRequest_with_interpolated_string()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            public class MyEndpoint
            {
                public BadRequest<string> Handle(string name) => TypedResults.BadRequest($"Invalid: {name}");
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            public class MyEndpoint
            {
                public BadRequest<string> Handle(string name) => TypedResults.Problem(detail: $"Invalid: {name}", statusCode: StatusCodes.Status400BadRequest);
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<TypedResultsBadRequestAnalyzer, TypedResultsBadRequestCodeFixProvider>(
            source, expected, ApiStubs);
    }
}
