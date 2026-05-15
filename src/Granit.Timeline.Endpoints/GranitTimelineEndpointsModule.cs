using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Timeline.Endpoints.Workspaces;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Timeline.Endpoints;

/// <summary>
/// Granit module for timeline HTTP endpoints.
/// Exposes the activity stream, entry management, and follower operations via Minimal API routes.
/// </summary>
/// <remarks>
/// Map endpoints in your application:
/// <code>
/// app.MapGranitTimeline();
/// </code>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitTimelineModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitTimelineEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddWorkspaceContribution<TimelineWorkspaceContribution>();
        context.Services.AddFeatureProvider<TimelineFeatureProvider>();
    }
}
