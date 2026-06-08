using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates HTTP response conventions for Minimal API endpoints.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class ApiConventionTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(ApiConventionTests).Assembly);

    [Fact]
    public void Endpoint_handlers_should_use_typed_results_union() =>
        ApiConventionRules.EndpointHandlersShouldUseTypedResults(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            // Returns dynamic shapes — incompatible with Results<> union types
            "QueryEndpointHandler",
            // Polymorphic status codes based on redelivery result
            "WebhookRedeliveryEndpoint",
            // Polymorphic returns pending refactoring
            "LocalizationEndpointRouteBuilderExtensions",
            // OIDC passthrough — Results.SignIn/Forbid/Challenge are protocol-level
            "ConnectAuthorizationEndpoints",
            "ConnectTokenEndpoints",
            "ConnectLogoutEndpoints",
            "ConnectUserInfoEndpoints",
            "ConnectVerifyEndpoints");

    [Fact]
    public void Complex_results_unions_should_include_ProblemHttpResult() =>
        ApiConventionRules.ComplexResultsUnionsShouldIncludeProblemHttpResult(
            Path.Join(RepoRoot, "src"),
            RepoRoot);

    [Fact]
    public void Endpoint_registrations_should_have_complete_OpenAPI_metadata() =>
        ApiConventionRules.EndpointRegistrationsShouldHaveCompleteOpenApiMetadata(
            Path.Join(RepoRoot, "src"),
            RepoRoot);

    [Fact]
    public void Public_Map_extension_methods_should_follow_MapGranit_convention() =>
        ApiConventionRules.PublicMapExtensionMethodsShouldFollowNamingConvention(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            requiredPrefix: "Granit",
            "MapGranitGroup");
}
