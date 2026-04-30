using Granit.Authorization;
using Granit.Authorization.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Authorization.Endpoints;

/// <summary>
/// Granit module for authorization management HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes permission management routes via
/// <see cref="Extensions.AuthorizationEndpointRouteBuilderExtensions.MapGranitAuthorization"/>.
/// Requires <see cref="GranitAuthorizationModule"/> for permission policy enforcement.
/// The application host must register an implementation of
/// <see cref="Abstractions.IPermissionManagerReader"/>/<see cref="Abstractions.IPermissionManagerWriter"/>
/// (e.g. via <c>[DependsOn(typeof(GranitAuthorizationEntityFrameworkCoreModule))]</c>).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitAuthorizationEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddWorkspaceContribution<AuthorizationWorkspaceContribution>();
}
