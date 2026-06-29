using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests.Abstractions.Tests;

/// <summary>
/// Behavioural tests for the centralised endpoint/OpenAPI rules in <see cref="ApiConventionRules"/>
/// that consumers (granit-business, website, iot) reuse — the 422 validation-status scan and the
/// bodyless-verb <c>[FromBody]</c> binding scan. Each test writes a synthetic <c>*Endpoints.cs</c>
/// file into a throwaway <c>src/</c> tree so the rules are validated without depending on the
/// framework source.
/// </summary>
public sealed class ApiConventionRulesTests : IDisposable
{
    private readonly string _root =
        Path.Join(Path.GetTempPath(), "granit-archtests-" + Guid.NewGuid().ToString("N"));

    private string Src => Path.Join(_root, "src");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ── ProducesValidationProblem → 422 ──────────────────────────────────────────────────────────

    [Fact]
    public void Bare_ProducesValidationProblem_fails()
    {
        WriteEndpoints("BareValidation", """
            group.MapPost("/", CreateAsync)
                .ProducesValidationProblem();
            """);

        Should.Throw<ShouldAssertException>(() =>
            ApiConventionRules.ProducesValidationProblemShouldTarget422(Src, _root));
    }

    [Theory]
    [InlineData("StatusCodes.Status422UnprocessableEntity")]
    [InlineData("422")]
    public void ProducesValidationProblem_targeting_422_passes(string arg)
    {
        WriteEndpoints("Validation422", $$"""
            group.MapPost("/", CreateAsync)
                .ProducesValidationProblem({{arg}});
            """);

        Should.NotThrow(() =>
            ApiConventionRules.ProducesValidationProblemShouldTarget422(Src, _root));
    }

    // ── Bodyless verb [FromBody] ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Bodyless_handler_without_FromBody_fails()
    {
        WriteEndpoints("DeleteNoBind", """
            group.MapDelete("/{id}", DeleteAsync);

            private static async Task<IResult> DeleteAsync(BlobDeleteRequest request)
            {
                return Results.NoContent();
            }
            """);

        Should.Throw<ShouldAssertException>(() =>
            ApiConventionRules.BodylessVerbHandlersShouldAnnotateRequestDtoParameters(Src, _root));
    }

    [Fact]
    public void Bodyless_handler_with_FromBody_passes()
    {
        WriteEndpoints("DeleteBound", """
            group.MapDelete("/{id}", DeleteAsync);

            private static async Task<IResult> DeleteAsync([FromBody] BlobDeleteRequest request)
            {
                return Results.NoContent();
            }
            """);

        Should.NotThrow(() =>
            ApiConventionRules.BodylessVerbHandlersShouldAnnotateRequestDtoParameters(Src, _root));
    }

    [Fact]
    public void Body_carrying_verb_with_unannotated_request_is_ignored()
    {
        WriteEndpoints("PostUnbound", """
            group.MapPost("/", CreateAsync);

            private static async Task<IResult> CreateAsync(BlobCreateRequest request)
            {
                return Results.Ok();
            }
            """);

        Should.NotThrow(() =>
            ApiConventionRules.BodylessVerbHandlersShouldAnnotateRequestDtoParameters(Src, _root));
    }

    private void WriteEndpoints(string module, string body)
    {
        string dir = Path.Join(Src, $"Granit.{module}.Endpoints");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Join(dir, $"{module}Endpoints.cs"),
            $$"""
            namespace Granit.{{module}}.Endpoints;

            public static class {{module}}Endpoints
            {
                public static void Map(RouteGroupBuilder group)
                {
            {{body}}
                }
            }
            """);
    }
}
