using Granit.Authentication.ApiKeys.Endpoints.Workspaces;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Authentication.ApiKeys.Endpoints;

/// <summary>
/// Module for API key management endpoints.
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitAuthenticationApiKeysEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddWorkspaceContribution<ApiKeysWorkspaceContribution>();
        context.Services.AddFeatureProvider<ApiKeysFeatureProvider>();
    }
}
