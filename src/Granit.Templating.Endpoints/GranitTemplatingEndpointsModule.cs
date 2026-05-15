using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Templating.Endpoints.Workspaces;
using Granit.Users;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Templating.Endpoints;

/// <summary>
/// Granit module for template administration HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes CRUD endpoints for template draft management under
/// <c>/api/v1/templates</c>, protected by the <c>Templates.Manage</c> permission.
/// <para>
/// The host application must call
/// <see cref="Extensions.TemplatingEndpointRouteBuilderExtensions.MapGranitTemplating"/>
/// to register the routes.
/// </para>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitTemplatingModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitTemplatingEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddFeatureProvider<TemplatingFeatureProvider>();
    }
}
