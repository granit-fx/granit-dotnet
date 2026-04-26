using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>Domain event raised when an external provider identifier is registered against a customer.</summary>
public sealed record CustomerExternalMappingAddedEvent(
    CustomerId CustomerId,
    Guid? TenantId,
    string ProviderName,
    string ExternalId) : IDomainEvent;
