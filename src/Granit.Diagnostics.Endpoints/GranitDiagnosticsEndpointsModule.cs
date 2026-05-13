using Granit.Authorization;
using Granit.Diagnostics.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Diagnostics.Endpoints;

/// <summary>
/// Granit module for diagnostics monitoring HTTP endpoints.
/// Exposes an aggregated health status endpoint for admin dashboards.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDiagnosticsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitDiagnosticsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddWorkspaceContribution<DiagnosticsWorkspaceContribution>();
}
