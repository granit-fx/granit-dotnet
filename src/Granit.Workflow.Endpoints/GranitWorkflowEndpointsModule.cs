using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workflow.Endpoints.Extensions;
using Granit.Workflow.Endpoints.Internal;
using Granit.Workflow.Endpoints.Workspaces;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Workflow.Endpoints;

/// <summary>
/// Granit module for workflow HTTP endpoints.
/// Exposes workflow transition history and status via Minimal API routes.
/// </summary>
/// <remarks>
/// <para>
/// Map endpoints in your application:
/// <code>
/// app.MapGranitWorkflow();
/// </code>
/// </para>
/// <para>
/// Register services:
/// <code>
/// services.AddGranitWorkflowEndpoints();
/// </code>
/// </para>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkflowModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitWorkflowEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<WorkflowEndpointsLocalizationResource>();
        context.Services.AddGranitWorkflowEndpoints();
        context.Services.AddFeatureProvider<WorkflowFeatureProvider>();
    }
}
