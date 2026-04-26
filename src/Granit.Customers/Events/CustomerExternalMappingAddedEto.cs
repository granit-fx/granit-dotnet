using Granit.Customers.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Customers.Events;

/// <summary>
/// Integration event published when a new external provider identifier is registered
/// against a customer. Provider modules consume this to confirm side-effects of an
/// external API call (e.g., when a Stripe customer is created and its <c>cus_xxx</c>
/// is persisted, the integration handler may push a follow-up <c>setup_intent</c>).
/// </summary>
public sealed record CustomerExternalMappingAddedEto(
    CustomerId CustomerId,
    Guid? TenantId,
    string ProviderName,
    string ExternalId) : IIntegrationEvent;
