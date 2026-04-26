using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>Domain event raised when a customer's billing identity (contact, address, currency) is updated.</summary>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped customers.</param>
public sealed record CustomerUpdatedEvent(
    CustomerId CustomerId,
    Guid? TenantId) : IDomainEvent;
