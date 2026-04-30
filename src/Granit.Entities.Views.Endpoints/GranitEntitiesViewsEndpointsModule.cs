using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Entities.Views.Endpoints;

/// <summary>
/// Granit module for the EntityView Minimal API endpoints. Wires the permission
/// definitions + the localisation resource. Hosts must call
/// <c>app.MapGranitEntityViewsEndpoints("/api/{version}/entities")</c> in their
/// pipeline configuration to mount the routes.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitEntitiesViewsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitEntitiesViewsEndpointsModule : GranitModule;
