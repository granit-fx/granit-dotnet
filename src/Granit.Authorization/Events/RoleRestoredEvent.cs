using Granit.Events;
using Granit.MultiTenancy;

namespace Granit.Authorization.Events;

/// <summary>
/// Raised when the client-role sync clears the <c>IsOrphaned</c> flag on a
/// <see cref="Domain.RoleMetadata"/> row because the upstream provider started returning
/// that role again (typical case: admin deleted, then re-added the client role in
/// Keycloak / Entra / Cognito between two sync runs).
/// </summary>
/// <param name="RoleId">Aggregate identifier.</param>
/// <param name="Name">Role name at the moment of restoration.</param>
/// <param name="MultiTenancySides">Side applicability (unchanged).</param>
/// <param name="TenantId">Tenant scope (unchanged).</param>
/// <param name="ClientId">OIDC client scope (unchanged).</param>
public sealed record RoleRestoredEvent(
    Guid RoleId,
    string Name,
    MultiTenancySides MultiTenancySides,
    Guid? TenantId,
    string? ClientId) : IDomainEvent;
