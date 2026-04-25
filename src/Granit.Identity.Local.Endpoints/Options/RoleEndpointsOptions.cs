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
    /// When <see langword="true"/> (default), tenant admins can create / rename / delete
    /// <see cref="Granit.MultiTenancy.MultiTenancySides.Tenant"/>-scoped roles in their
    /// own tenant. Host and Both roles remain host-admin only regardless of this flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires <c>TenantAwareRoleLookupNormalizer</c> to be registered as the
    /// <c>ILookupNormalizer</c>, otherwise two tenants with the same role display name
    /// collide on the ASP.NET Core Identity <c>AspNetRoles.NormalizedName</c> unique
    /// index. The normalizer is wired automatically by
    /// <c>Granit.Identity.Local.AspNetIdentity</c>.
    /// </para>
    /// <para>
    /// Set to <see langword="false"/> to opt out and have the endpoints refuse
    /// Tenant-scope create requests with 403 (useful for host-only deployments that
    /// want to keep the attack surface minimal).
    /// </para>
    /// </remarks>
    public bool AllowTenantRoles { get; set; } = true;
}
