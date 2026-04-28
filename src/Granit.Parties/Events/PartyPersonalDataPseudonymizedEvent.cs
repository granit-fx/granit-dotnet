using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when a party's PII is pseudonymised under a GDPR Article 17 erasure request.</summary>
public sealed record PartyPersonalDataPseudonymizedEvent(PartyId PartyId, Guid? TenantId) : IDomainEvent;
