using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>
/// Integration event published when a new customer is created — consumed by downstream
/// modules (Invoicing, Subscriptions, Payments) that need to react to customer creation
/// (e.g., to provision a default payment method placeholder, or seed metering counters).
/// </summary>
/// <param name="CustomerId">Unique customer identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped customers.</param>
/// <param name="LegalName">Legal name as captured at creation time.</param>
/// <param name="DefaultCurrency">ISO 4217 currency code.</param>
public sealed record CustomerCreatedEto(
    CustomerId CustomerId,
    Guid? TenantId,
    string LegalName,
    string DefaultCurrency) : IIntegrationEvent;
