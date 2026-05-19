using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class UntypedResultsCodeFixProviderTests
{
    private static readonly string[] ApiStubs = [Analyzers.Tests.AnalyzerTestHelpers.MinimalApiResultsStub];

    [Fact]
    public async Task Replaces_Results_Ok_with_TypedResults_Ok()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public class MyEndpoint
            {
                public IResult Handle() => Results.Ok();
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            public class MyEndpoint
            {
                public IResult Handle() => TypedResults.Ok();
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<UntypedResultsAnalyzer, UntypedResultsCodeFixProvider>(
            source, expected, ApiStubs);
    }

    [Fact]
    public async Task Replaces_Results_BadRequest_with_TypedResults_BadRequest()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public class MyEndpoint
            {
                public IResult Handle() => Results.BadRequest("error");
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            public class MyEndpoint
            {
                public IResult Handle() => TypedResults.BadRequest("error");
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<UntypedResultsAnalyzer, UntypedResultsCodeFixProvider>(
            source, expected, ApiStubs);
    }

    [Fact]
    public async Task Replaces_Results_NotFound_with_TypedResults_NotFound()
    {
        string source = """
            using Microsoft.AspNetCore.Http;
            public class MyEndpoint
            {
                public IResult Handle() => Results.NotFound();
            }
            """;

        string expected = """
            using Microsoft.AspNetCore.Http;
            public class MyEndpoint
            {
                public IResult Handle() => TypedResults.NotFound();
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<UntypedResultsAnalyzer, UntypedResultsCodeFixProvider>(
            source, expected, ApiStubs);
    }
}
