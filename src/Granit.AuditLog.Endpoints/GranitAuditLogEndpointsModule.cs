using Granit.AuditLog;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;

namespace Granit.AuditLog.Endpoints;

/// <summary>
/// Granit module for audit log read-only Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-map routes. The host application must call
/// <c>app.MapAuditLogEndpoints()</c> in the pipeline configuration.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitAuditLogModule))]
public sealed class GranitAuditLogEndpointsModule : GranitModule;
