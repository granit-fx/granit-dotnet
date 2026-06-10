namespace Granit.MultiTenancy.Endpoints.Options;

/// <summary>
/// Options for multi-tenancy management endpoints.
/// </summary>
public sealed class MultiTenancyEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MultiTenancy:Endpoints";

    /// <summary>
    /// Route prefix for all tenant management endpoints.
    /// Default: <c>"multi-tenancy"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "multi-tenancy";

    /// <summary>
    /// OpenAPI tag name for all tenant management endpoints.
    /// Default: <c>"Multi-Tenancy - Tenants"</c>.
    /// </summary>
    public string TagName { get; set; } = "Multi-Tenancy - Tenants";
}
