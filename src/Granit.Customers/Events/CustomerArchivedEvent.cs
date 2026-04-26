using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>Domain event raised when a customer is archived (terminal state).</summary>
public sealed record CustomerArchivedEvent(CustomerId CustomerId, Guid? TenantId) : IDomainEvent;
