using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Authentication.ApiKeys.Endpoints;

/// <summary>
/// Module for API key management endpoints.
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitValidationModule))]
public sealed class GranitAuthenticationApiKeysEndpointsModule : GranitModule;
