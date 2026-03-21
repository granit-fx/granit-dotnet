using Microsoft.AspNetCore.Identity;

namespace Granit.OpenIddict.Entities;

/// <summary>
/// Application role entity extending ASP.NET Core Identity's <see cref="IdentityRole{TKey}"/>
/// with an optional description.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GranitRole"/> is intentionally <b>not</b> tenant-scoped
/// (<c>IMultiTenant</c> is not implemented). Roles are shared globally across all
/// tenants so that a single RBAC matrix applies platform-wide. Tenant-specific
/// access is enforced at the permission/policy level, not at the role definition level.
/// </para>
/// <para>
/// This differs from <see cref="GranitUser"/>, <see cref="Granit.OpenIddict.Domain.GranitUserGroup"/>,
/// and <see cref="Granit.OpenIddict.Domain.GranitUserGroupMember"/> which are all tenant-scoped.
/// </para>
/// </remarks>
public class GranitRole : IdentityRole<Guid>
{
    /// <summary>Gets or sets a human-readable description for the role (max 512 characters).</summary>
    public string? Description { get; set; }
}
