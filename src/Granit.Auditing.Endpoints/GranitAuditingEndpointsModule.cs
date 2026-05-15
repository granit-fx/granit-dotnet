using Granit.Auditing;
using Granit.Auditing.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Auditing.Endpoints;

/// <summary>
/// Granit module for audit log read-only Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-map routes. The host application must call
/// <c>app.MapGranitAuditing()</c> in the pipeline configuration.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitAuditingEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddWorkspaceContribution<AuditingWorkspaceContribution>();
        context.Services.AddFeatureProvider<AuditingFeatureProvider>();
    }
}
