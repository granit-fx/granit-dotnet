using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>
/// Integration event published when a new external provider identifier is registered
/// against a contact. Provider modules consume this to confirm side-effects of an
/// external API call (e.g., a Stripe customer created and its <c>cus_xxx</c> persisted).
/// </summary>
public sealed record PartyExternalMappingAddedEto(
    PartyId PartyId,
    Guid? TenantId,
    string ProviderName,
    string ExternalId) : IIntegrationEvent;
