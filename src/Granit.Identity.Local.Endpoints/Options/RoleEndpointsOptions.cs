namespace Granit.Identity.Local.Endpoints.Options;

/// <summary>
/// Options for the local role CRUD endpoints.
/// </summary>
public sealed class RoleEndpointsOptions
{
    /// <summary>Route prefix for the role CRUD endpoints. Default: <c>"admin/roles"</c>.</summary>
    public string RolesRoutePrefix { get; set; } = "admin/roles";

    /// <summary>OpenAPI tag for the role CRUD endpoints. Default: <c>"Identity - Roles"</c>.</summary>
    public string TagName { get; set; } = "Identity - Roles";
}
