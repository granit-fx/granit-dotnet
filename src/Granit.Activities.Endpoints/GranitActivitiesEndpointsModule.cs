using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Activities.Endpoints;

/// <summary>
/// Granit module for activity HTTP endpoints — exposes the polymorphic
/// to-do CRUD + lifecycle transitions via Minimal API.
/// </summary>
/// <remarks>
/// Map endpoints in your application:
/// <code>app.MapGranitActivities();</code>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitActivitiesModule),
    typeof(GranitValidationModule))]
public sealed class GranitActivitiesEndpointsModule : GranitModule;
