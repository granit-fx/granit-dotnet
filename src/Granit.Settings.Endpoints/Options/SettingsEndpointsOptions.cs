namespace Granit.Settings.Endpoints.Options;

/// <summary>
/// Options for settings endpoints.
/// </summary>
public sealed class SettingsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Settings:Endpoints";

    /// <summary>
    /// Route prefix for user-scoped setting endpoints.
    /// Default: <c>"settings/user"</c>.
    /// </summary>
    public string UserRoutePrefix { get; set; } = "settings/user";

    /// <summary>
    /// Route prefix for global setting endpoints.
    /// Default: <c>"settings/global"</c>.
    /// </summary>
    public string GlobalRoutePrefix { get; set; } = "settings/global";

    /// <summary>
    /// Route prefix for tenant-scoped setting endpoints.
    /// Default: <c>"settings/tenant"</c>.
    /// </summary>
    public string TenantRoutePrefix { get; set; } = "settings/tenant";

    /// <summary>
    /// OpenAPI tag name for global setting administration endpoints.
    /// Default: <c>"Settings - Global"</c>.
    /// </summary>
    public string GlobalTagName { get; set; } = "Settings - Global";

    /// <summary>
    /// OpenAPI tag name for tenant-scoped setting administration endpoints.
    /// Default: <c>"Settings - Tenant"</c>.
    /// </summary>
    public string TenantTagName { get; set; } = "Settings - Tenant";

    /// <summary>
    /// OpenAPI tag name for user-scoped setting endpoints.
    /// Default: <c>"Settings - User"</c>.
    /// </summary>
    public string UserTagName { get; set; } = "Settings - User";
}
