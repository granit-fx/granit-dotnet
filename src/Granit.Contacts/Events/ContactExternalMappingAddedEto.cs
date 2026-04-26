using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>
/// Integration event published when a new external provider identifier is registered
/// against a contact. Provider modules consume this to confirm side-effects of an
/// external API call (e.g., a Stripe customer created and its <c>cus_xxx</c> persisted).
/// </summary>
public sealed record ContactExternalMappingAddedEto(
    ContactId ContactId,
    Guid? TenantId,
    string ProviderName,
    string ExternalId) : IIntegrationEvent;
