using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>Domain event raised when a new <c>Customer</c> aggregate is created.</summary>
/// <param name="CustomerId">Unique customer identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped customers.</param>
/// <param name="LegalName">Legal name as captured at creation time.</param>
public sealed record CustomerCreatedEvent(
    CustomerId CustomerId,
    Guid? TenantId,
    string LegalName) : IDomainEvent;
