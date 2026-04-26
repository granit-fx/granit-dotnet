using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>
/// Integration event for customer reactivation — downstream consumers may resume
/// dunning, restore payment methods, or unfreeze active subscriptions.
/// </summary>
public sealed record CustomerActivatedEto(CustomerId CustomerId, Guid? TenantId) : IIntegrationEvent;
