using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>
/// Integration event for customer archival — downstream consumers should cancel any
/// active subscriptions, void any draft invoices, and stop accepting new transactions.
/// The customer row is preserved (legal retention).
/// </summary>
public sealed record CustomerArchivedEto(CustomerId CustomerId, Guid? TenantId) : IIntegrationEvent;
