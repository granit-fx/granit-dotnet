using Granit.Events;
using Granit.MultiTenancy;

namespace Granit.Authorization.Events;

/// <summary>
/// Raised when the client-role sync pipeline flips <c>IsOrphaned</c> to <see langword="true"/>
/// on an existing <see cref="Domain.RoleMetadata"/> row because its upstream counterpart
/// (Keycloak client role, Entra App Role, Cognito group) is no longer returned by the
/// provider.
/// </summary>
/// <remarks>
/// Only raised by the <see cref="OrphanedRolePolicy.SoftDelete"/> path. The
/// <see cref="OrphanedRolePolicy.HardDelete"/> path raises <see cref="RoleDeletedEvent"/>
/// instead (same signal as an admin-triggered delete).
/// </remarks>
/// <param name="RoleId">Aggregate identifier.</param>
/// <param name="Name">Role name at the moment of orphaning.</param>
/// <param name="MultiTenancySide">Side applicability (unchanged).</param>
/// <param name="TenantId">Tenant scope (unchanged).</param>
/// <param name="ClientId">OIDC client scope (unchanged) — the sync's <c>TrackedClientId</c> key.</param>
/// <param name="OrphanedAt">Timestamp captured by the sync when the flag was flipped.</param>
public sealed record RoleOrphanedEvent(
    Guid RoleId,
    string Name,
    MultiTenancySide MultiTenancySide,
    Guid? TenantId,
    string? ClientId,
    DateTimeOffset OrphanedAt) : IDomainEvent;
