using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that minimal API handlers bound to bodyless HTTP verbs
/// (<c>DELETE</c>, <c>GET</c>, <c>HEAD</c>) carry an explicit <c>[FromBody]</c>
/// on any complex request DTO parameter.
/// </summary>
/// <remarks>
/// ASP.NET Core 10 rejects an inferred body parameter on these verbs at endpoint construction
/// (<c>InvalidOperationException: Body was inferred but the method does not allow inferred body
/// parameters</c>), so a missing <c>[FromBody]</c> is a deployment-time regression that escapes
/// compile-time checks. The scan logic lives in
/// <see cref="ApiConventionRules.BodylessVerbHandlersShouldAnnotateRequestDtoParameters"/>
/// (Granit.ArchitectureTests.Abstractions) so downstream repos can reuse it.
/// </remarks>
public sealed class BodylessVerbBodyBindingTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(BodylessVerbBodyBindingTests).Assembly);

    [Fact]
    public void Bodyless_verb_handlers_must_annotate_request_dto_parameters_with_FromBody() =>
        ApiConventionRules.BodylessVerbHandlersShouldAnnotateRequestDtoParameters(
            Path.Join(RepoRoot, "src"),
            RepoRoot);
}
