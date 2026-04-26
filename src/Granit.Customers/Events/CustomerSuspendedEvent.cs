using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>Domain event raised when a customer is suspended.</summary>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped customers.</param>
/// <param name="Reason">Optional free-text justification (e.g., <c>"unpaid balance"</c>, <c>"fraud review"</c>).</param>
public sealed record CustomerSuspendedEvent(
    CustomerId CustomerId,
    Guid? TenantId,
    string? Reason) : IDomainEvent;
