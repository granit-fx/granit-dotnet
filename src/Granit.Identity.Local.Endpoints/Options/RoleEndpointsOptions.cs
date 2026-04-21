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
    /// When <see langword="false"/> (default), create / rename / delete endpoints refuse
    /// <see cref="Granit.MultiTenancy.MultiTenancySide.Tenant"/> role requests — only
    /// host-level and Both roles are creatable.
    /// </summary>
    /// <remarks>
    /// Opt-in requires <c>TenantAwareRoleLookupNormalizer</c> to be registered as the
    /// <c>ILookupNormalizer</c>; otherwise two tenants with the same role display name
    /// collide on the ASP.NET Core Identity <c>AspNetRoles.NormalizedName</c> unique index.
    /// </remarks>
    public bool AllowTenantRoles { get; set; }
}
