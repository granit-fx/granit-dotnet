namespace Granit.MultiTenancy.Endpoints.Options;

/// <summary>
/// Options for multi-tenancy management endpoints.
/// </summary>
public sealed class MultiTenancyEndpointsOptions
{
    /// <summary>
    /// Route prefix for all tenant management endpoints.
    /// Default: <c>"admin/tenants"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "admin/tenants";

    /// <summary>
    /// OpenAPI tag name for all tenant management endpoints.
    /// Default: <c>"Platform - Tenants"</c>.
    /// </summary>
    public string TagName { get; set; } = "Platform - Tenants";
}
