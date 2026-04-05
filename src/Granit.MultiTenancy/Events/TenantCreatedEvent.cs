using Granit.Events;

namespace Granit.MultiTenancy.Events;

/// <summary>
/// Raised when a new tenant is created.
/// </summary>
/// <param name="TenantId">The unique identifier of the tenant.</param>
/// <param name="Name">Display name of the tenant.</param>
/// <param name="Identifier">Unique slug/subdomain identifier.</param>
public sealed record TenantCreatedEvent(
    Guid TenantId,
    string Name,
    string Identifier) : IDomainEvent;
