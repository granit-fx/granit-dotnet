using Granit.Events;
using Granit.MultiTenancy;

namespace Granit.Authorization.Events;

/// <summary>
/// Raised when an existing <see cref="Domain.RoleMetadata"/> is renamed or its description
/// changes. Side and tenant scope are immutable after creation.
/// </summary>
/// <param name="RoleId">Aggregate identifier.</param>
/// <param name="Name">New role name.</param>
/// <param name="PreviousName">
/// Name the role had before the update, when it was renamed. <see langword="null"/>
/// when only the description changed (name was not renamed). Cache invalidation
/// handlers use this to flush entries keyed on the stale name.
/// </param>
/// <param name="MultiTenancySide">Side applicability (unchanged since creation).</param>
/// <param name="TenantId">Tenant scope (unchanged since creation).</param>
/// <param name="ClientId">OIDC client scope (unchanged since creation).</param>
public sealed record RoleUpdatedEvent(
    Guid RoleId,
    string Name,
    string? PreviousName,
    MultiTenancySide MultiTenancySide,
    Guid? TenantId,
    string? ClientId) : IDomainEvent;
