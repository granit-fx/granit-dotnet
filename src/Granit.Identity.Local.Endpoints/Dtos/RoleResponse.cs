using Granit.MultiTenancy;

namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>Role detail payload returned by the CRUD endpoints.</summary>
/// <param name="Id">Aggregate identifier.</param>
/// <param name="Name">Role display name.</param>
/// <param name="MultiTenancySide">Host / Tenant / Both applicability.</param>
/// <param name="TenantId">Tenant scope (null for Host / Both).</param>
/// <param name="ClientId">OIDC client scope — reserved for future realm / client role distinction; currently always null.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IsSystem"><c>true</c> for platform-seeded roles (cannot be renamed or deleted).</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="ModifiedAt">Last modification timestamp.</param>
public sealed record RoleResponse(
    Guid Id,
    string Name,
    MultiTenancySide MultiTenancySide,
    Guid? TenantId,
    string? ClientId,
    string? Description,
    bool IsSystem,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
