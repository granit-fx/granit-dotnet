using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that all interface-typed parameters in endpoint handlers are decorated
/// with <c>[FromServices]</c>.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class EndpointParameterBindingTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(EndpointParameterBindingTests).Assembly);

    [Fact]
    public void Endpoint_service_parameters_must_have_FromServices() =>
        EndpointParameterBindingRules.EndpointServiceParametersMustHaveFromServices(
            Path.Join(RepoRoot, "src"),
            RepoRoot);
}
