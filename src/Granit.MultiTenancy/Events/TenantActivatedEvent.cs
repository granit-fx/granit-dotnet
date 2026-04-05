using Granit.Events;

namespace Granit.MultiTenancy.Events;

/// <summary>
/// Raised when a tenant is activated.
/// </summary>
/// <param name="TenantId">The unique identifier of the tenant.</param>
public sealed record TenantActivatedEvent(Guid TenantId) : IDomainEvent;
