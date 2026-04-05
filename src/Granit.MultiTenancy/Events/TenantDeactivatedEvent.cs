using Granit.Events;

namespace Granit.MultiTenancy.Events;

/// <summary>
/// Raised when a tenant is deactivated.
/// Can be used to trigger session invalidation or access revocation.
/// </summary>
/// <param name="TenantId">The unique identifier of the tenant.</param>
public sealed record TenantDeactivatedEvent(Guid TenantId) : IDomainEvent;
