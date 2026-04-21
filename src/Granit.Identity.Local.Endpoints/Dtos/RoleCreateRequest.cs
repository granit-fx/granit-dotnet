using Granit.MultiTenancy;

namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>Request to create a new local role.</summary>
/// <param name="Name">Display name of the role (max 256 chars).</param>
/// <param name="MultiTenancySide">
/// Host / Tenant / Both applicability. <see cref="MultiTenancySide.Tenant"/> is refused
/// unless <c>RoleEndpointsOptions.AllowTenantRoles</c> is enabled.
/// </param>
/// <param name="TenantId">
/// Tenant identifier — required iff <paramref name="MultiTenancySide"/> is
/// <see cref="MultiTenancySide.Tenant"/>. Must match the caller's tenant context when
/// invoked from a tenant admin.
/// </param>
/// <param name="Description">Optional description (max 2048 chars).</param>
public sealed record RoleCreateRequest(
    string Name,
    MultiTenancySide MultiTenancySide,
    Guid? TenantId,
    string? Description);
