using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>Domain event raised when a customer transitions from <c>Suspended</c> back to <c>Active</c>.</summary>
public sealed record CustomerActivatedEvent(CustomerId CustomerId, Guid? TenantId) : IDomainEvent;
