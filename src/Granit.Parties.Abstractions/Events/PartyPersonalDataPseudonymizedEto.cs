using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Integration event signalling that a contact's PII has been pseudonymised (GDPR Article 17).</summary>
public sealed record PartyPersonalDataPseudonymizedEto(PartyId PartyId, Guid? TenantId) : IIntegrationEvent;
