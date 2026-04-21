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

    /// <summary>
    /// When <see langword="false"/> (Phase 1 default), create / rename / delete endpoints
    /// refuse <see cref="Granit.MultiTenancy.MultiTenancySide.Tenant"/> role requests — only
    /// host-level and Both roles are creatable. Flip to <see langword="true"/> once
    /// <c>TenantAwareRoleLookupNormalizer</c> is wired (Phase 2) so tenant-scoped roles
    /// can coexist without colliding on the ASP.NET Core Identity
    /// <c>AspNetRoles.NormalizedName</c> unique index.
    /// </summary>
    public bool AllowTenantRoles { get; set; }
}
