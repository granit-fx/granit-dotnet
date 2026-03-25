namespace Granit.AuditLog.Endpoints.Options;

/// <summary>
/// Configuration options for audit log API endpoints.
/// </summary>
public sealed class AuditLogEndpointsOptions
{
    /// <summary>
    /// Route prefix for audit log endpoints. Default: <c>"audit-log"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "audit-log";

    /// <summary>
    /// OpenAPI tag name for the audit log endpoints.
    /// Default: <c>"Audit Log"</c>.
    /// </summary>
    public string TagName { get; set; } = "Audit Log";
}
