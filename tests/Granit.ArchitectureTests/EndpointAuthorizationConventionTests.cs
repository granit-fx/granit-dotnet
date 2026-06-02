using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces the central authorization principle: every Minimal API endpoint must
/// declare an explicit authorization stance.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class EndpointAuthorizationConventionTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(EndpointAuthorizationConventionTests).Assembly);

    [Fact]
    public void Every_Map_endpoint_must_declare_authorization() =>
        EndpointAuthorizationRules.EveryEndpointMustDeclareAuthorizationStance(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            // OIDC protocol endpoints — authorization enforced by OpenIddict's own pipeline
            "ConnectAuthorizationEndpoints",
            "ConnectTokenEndpoints",
            "ConnectLogoutEndpoints",
            "ConnectUserInfoEndpoints",
            "ConnectIntrospectionEndpoints",
            "ConnectRevocationEndpoints");
}
