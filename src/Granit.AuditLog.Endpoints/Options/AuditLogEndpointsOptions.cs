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
    /// Authorization policy name for all audit log endpoints.
    /// Default: <c>"AuditLog.Read"</c>.
    /// </summary>
    public string AuthorizationPolicy { get; set; } = "AuditLog.Read";

    /// <summary>
    /// Role required for audit log access (used as fallback when the policy is not explicitly configured).
    /// Default: <c>"granit-audit-log-admin"</c>.
    /// </summary>
    public string RequiredRole { get; set; } = "granit-audit-log-admin";

    /// <summary>
    /// OpenAPI tag name for the audit log endpoints.
    /// Default: <c>"Audit Log"</c>.
    /// </summary>
    public string TagName { get; set; } = "Audit Log";
}
