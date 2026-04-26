using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>
/// Integration event published when a customer's billing identity is updated.
/// Downstream modules listen for this to refresh cached display values (e.g., the
/// invoicing UI's "billed to" panel) or to push the new contact email to external
/// providers (Stripe customer.email update).
/// </summary>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="TenantId">Owning tenant id, or <c>null</c> for host-scoped customers.</param>
public sealed record CustomerUpdatedEto(
    CustomerId CustomerId,
    Guid? TenantId) : IIntegrationEvent;
