using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Domain event raised when an external provider identifier is registered against a contact.</summary>
public sealed record PartyExternalMappingAddedEvent(
    PartyId PartyId,
    Guid? TenantId,
    string ProviderName,
    string ExternalId) : IDomainEvent;
