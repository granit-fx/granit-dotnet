using Granit.Authorization;
using Granit.Diagnostics.Endpoints.Internal;
using Granit.Diagnostics.Endpoints.Options;
using Granit.Diagnostics.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Http.SecurityHeaders;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Diagnostics.Endpoints;

/// <summary>
/// Granit module for diagnostics monitoring HTTP endpoints.
/// Exposes an aggregated health status endpoint for admin dashboards.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDiagnosticsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpSecurityHeadersModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitDiagnosticsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<DiagnosticsEndpointsLocalizationResource>();

        context.Services
            .AddOptions<SecurityHeadersAuditOptions>()
            .BindConfiguration(SecurityHeadersAuditOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        context.Services.AddFeatureProvider<DiagnosticsFeatureProvider>();
    }
}
