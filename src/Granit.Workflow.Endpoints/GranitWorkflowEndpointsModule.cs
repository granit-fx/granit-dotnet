using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workflow.Endpoints.Extensions;

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
    typeof(GranitWorkflowModule))]
public sealed class GranitWorkflowEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitWorkflowEndpoints();
}
