using Granit.Auditing;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Auditing.Endpoints;

/// <summary>
/// Granit module for audit log read-only Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-map routes. The host application must call
/// <c>app.MapAuditingEndpoints()</c> in the pipeline configuration.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitAuditingEndpointsModule : GranitModule;
