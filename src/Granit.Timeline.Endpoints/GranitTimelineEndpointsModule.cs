using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Timeline.Endpoints;

/// <summary>
/// Granit module for timeline HTTP endpoints.
/// Exposes the activity stream, entry management, and follower operations via Minimal API routes.
/// </summary>
/// <remarks>
/// Map endpoints in your application:
/// <code>
/// app.MapTimelineEndpoints();
/// </code>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitTimelineModule),
    typeof(GranitValidationModule))]
public sealed class GranitTimelineEndpointsModule : GranitModule;
