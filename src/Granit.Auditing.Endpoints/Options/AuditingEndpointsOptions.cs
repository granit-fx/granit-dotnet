namespace Granit.Auditing.Endpoints.Options;

/// <summary>
/// Configuration options for audit log API endpoints.
/// </summary>
public sealed class AuditingEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auditing:Endpoints";

    /// <summary>
    /// Route prefix for audit log endpoints. Default: <c>"auditing"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "auditing";

    /// <summary>
    /// OpenAPI tag name for the audit log endpoints.
    /// Default: <c>"Audit Log"</c>.
    /// </summary>
    public string TagName { get; set; } = "Audit Log";
}
