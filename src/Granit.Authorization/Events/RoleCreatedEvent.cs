using Granit.Events;
using Granit.MultiTenancy;

namespace Granit.Authorization.Events;

/// <summary>
/// Raised when a new <see cref="Domain.RoleMetadata"/> aggregate is created.
/// </summary>
/// <remarks>
/// Domain event — dispatched after <c>SaveChanges</c> commits by
/// <c>DomainEventDispatcherInterceptor</c>.
/// </remarks>
/// <param name="RoleId">Aggregate identifier.</param>
/// <param name="Name">Role name.</param>
/// <param name="MultiTenancySide">Side applicability declared at creation.</param>
/// <param name="TenantId">Tenant scope (<see langword="null"/> for <see cref="MultiTenancySide.Host"/> / <see cref="MultiTenancySide.Both"/>).</param>
/// <param name="ClientId">OIDC client scope (<see langword="null"/> for realm / global roles).</param>
public sealed record RoleCreatedEvent(
    Guid RoleId,
    string Name,
    MultiTenancySide MultiTenancySide,
    Guid? TenantId,
    string? ClientId) : IDomainEvent;
