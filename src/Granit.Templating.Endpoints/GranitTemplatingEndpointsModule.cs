using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Security;
using Granit.Validation;

namespace Granit.Templating.Endpoints;

/// <summary>
/// Granit module for template administration HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes CRUD endpoints for template draft management under
/// <c>/api/v1/templates</c>, protected by the <c>Templates.Manage</c> permission.
/// <para>
/// The host application must call
/// <see cref="Extensions.TemplatingEndpointRouteBuilderExtensions.MapGranitTemplatingAdmin"/>
/// to register the routes.
/// </para>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitSecurityModule),
    typeof(GranitTemplatingModule),
    typeof(GranitValidationModule))]
public sealed class GranitTemplatingEndpointsModule : GranitModule;
