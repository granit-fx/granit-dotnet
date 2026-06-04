using Granit.MultiTenancy;

namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>Request to create a new local role.</summary>
/// <param name="Name">Display name of the role (max 256 chars).</param>
/// <param name="MultiTenancySides">
/// Host / Tenant / Both applicability. <see cref="MultiTenancySides.Tenant"/> is refused
/// when <c>RoleEndpointsOptions.AllowTenantRoles</c> is explicitly disabled (enabled by
/// default).
/// </param>
/// <param name="TenantId">
/// Tenant identifier — required iff <paramref name="MultiTenancySides"/> is
/// <see cref="MultiTenancySides.Tenant"/>. Must match the caller's tenant context when
/// invoked from a tenant admin.
/// </param>
/// <param name="Description">Optional description (max 2048 chars).</param>
public sealed record RoleCreateRequest(
    string Name,
    MultiTenancySides MultiTenancySides,
    Guid? TenantId,
    string? Description = null);
