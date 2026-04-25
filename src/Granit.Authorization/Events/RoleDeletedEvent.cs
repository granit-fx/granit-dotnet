using Granit.Events;
using Granit.MultiTenancy;

namespace Granit.Authorization.Events;

/// <summary>
/// Raised when a <see cref="Domain.RoleMetadata"/> is hard-deleted.
/// </summary>
/// <remarks>
/// Consumers should cascade invalidation of any permission grants or user assignments
/// keyed on <paramref name="RoleId"/> / <paramref name="Name"/>.
/// </remarks>
/// <param name="RoleId">Aggregate identifier.</param>
/// <param name="Name">Name at deletion time.</param>
/// <param name="MultiTenancySides">Side applicability.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="ClientId">OIDC client scope.</param>
public sealed record RoleDeletedEvent(
    Guid RoleId,
    string Name,
    MultiTenancySides MultiTenancySides,
    Guid? TenantId,
    string? ClientId) : IDomainEvent;
